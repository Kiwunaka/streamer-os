#include "visual_runtime_state.h"

#include <algorithm>
#include <QJsonArray>
#include <QJsonValue>

QDateTime VisualRuntimeState::RuntimeEvent::ExpireAt() const
{
    if (!activated_at.isValid()) {
        return queued_at.addMSecs(std::max(ttl_ms, min_display_ms));
    }
    return activated_at.addMSecs(std::max(ttl_ms, min_display_ms));
}

bool VisualRuntimeState::EnqueueEnvelope(const QJsonObject& envelope, QString* error)
{
    RuntimeEvent event;
    if (!ParseEnvelope(envelope, &event, error)) {
        return false;
    }

    const QDateTime now = QDateTime::currentDateTimeUtc();
    if (event.type == "overlay.hide.v1") {
        const QString key = event.payload.value("replaceKey").toString();
        if (key.isEmpty()) {
            if (error) *error = "overlay.hide.v1 requires payload.replaceKey.";
            return false;
        }
        pending_.erase(std::remove_if(pending_.begin(), pending_.end(), [&key](const RuntimeEvent& item) {
            return item.replace_key == key;
        }), pending_.end());
        if (active_.replace_key == key) active_ = RuntimeEvent();
        PromoteIfNeeded();
        return true;
    }
    if (!event.cooldown_key.isEmpty() && cooldowns_.contains(event.cooldown_key) && cooldowns_[event.cooldown_key] > now) {
        return true;
    }

    if (!event.replace_key.isEmpty()) {
        for (int i = pending_.size() - 1; i >= 0; --i) {
            if (pending_[i].replace_key == event.replace_key) {
                pending_.removeAt(i);
            }
        }

        if (active_.IsValid() && active_.replace_key == event.replace_key && active_.interruptible &&
            now >= active_.activated_at.addMSecs(active_.min_display_ms)) {
            event.activated_at = now;
            active_ = event;
            if (!event.cooldown_key.isEmpty() && event.cooldown_ms > 0) {
                cooldowns_[event.cooldown_key] = now.addMSecs(event.cooldown_ms);
            }
            return true;
        }
    }

    pending_.append(event);
    std::stable_sort(pending_.begin(), pending_.end(), [](const RuntimeEvent& left, const RuntimeEvent& right) {
        return HigherPriority(left, right);
    });
    PromoteIfNeeded();
    return true;
}

QJsonObject VisualRuntimeState::Snapshot()
{
    PromoteIfNeeded();

    QJsonObject out;
    out.insert("status", "ok");
    out.insert("pendingCount", pending_.size());
    if (active_.IsValid()) {
        out.insert("activeEvent", ToJson(active_));
    } else {
        out.insert("activeEvent", QJsonValue::Null);
    }
    return out;
}

void VisualRuntimeState::Clear()
{
    active_ = RuntimeEvent();
    pending_.clear();
    cooldowns_.clear();
}

bool VisualRuntimeState::ParseEnvelope(const QJsonObject& envelope, RuntimeEvent* out, QString* error) const
{
    if (!out) {
        return false;
    }

    const QString type = envelope.value("type").toString().trimmed();
    const int version = envelope.value("version").toInt(0);
    const QString event_id = envelope.value("eventId").toString().trimmed();
    const QJsonObject payload = envelope.value("payload").toObject();

    if (type.isEmpty() || !type.endsWith(".v1") || version != 1 || event_id.isEmpty()) {
        if (error) {
            *error = "Invalid runtime event envelope.";
        }
        return false;
    }

    const QJsonObject queue = envelope.value("queue").toObject();
    out->type = type;
    out->version = version;
    out->event_id = event_id;
    out->timestamp = envelope.value("timestamp").toString();
    out->source = envelope.value("source").toString("plugin-client");
    out->target = envelope.value("target").toString("runtime");
    out->priority = envelope.value("priority").toInt(50);
    out->ttl_ms = std::max(1000, envelope.value("ttlMs").toInt(8000));
    out->min_display_ms = std::max(0, queue.value("minDisplayMs").toInt(1200));
    out->interruptible = queue.contains("interruptible") ? queue.value("interruptible").toBool(true) : true;
    out->replace_key = queue.value("replaceByKey").toString();
    out->cooldown_key = queue.value("cooldownKey").toString();
    out->cooldown_ms = std::max(0, queue.value("cooldownMs").toInt(0));
    out->payload = payload;
    out->queued_at = QDateTime::currentDateTimeUtc();
    return true;
}

void VisualRuntimeState::PromoteIfNeeded()
{
    const QDateTime now = QDateTime::currentDateTimeUtc();

    pending_.erase(std::remove_if(pending_.begin(), pending_.end(), [&now](const RuntimeEvent& item) {
        return item.ExpireAt() <= now;
    }), pending_.end());
    for (auto it = cooldowns_.begin(); it != cooldowns_.end();) {
        if (it.value() <= now) it = cooldowns_.erase(it);
        else ++it;
    }

    if (active_.IsValid() && active_.ExpireAt() <= now) {
        active_ = RuntimeEvent();
    }

    if (active_.IsValid() && active_.interruptible && !pending_.isEmpty() &&
        now >= active_.activated_at.addMSecs(active_.min_display_ms) &&
        (pending_.first().priority > active_.priority ||
         (!active_.replace_key.isEmpty() && pending_.first().replace_key == active_.replace_key))) {
        active_ = RuntimeEvent();
    }

    if (!active_.IsValid() && !pending_.isEmpty()) {
        RuntimeEvent next = pending_.takeFirst();
        next.activated_at = now;
        active_ = next;
        if (!next.cooldown_key.isEmpty() && next.cooldown_ms > 0) {
            cooldowns_[next.cooldown_key] = now.addMSecs(next.cooldown_ms);
        }
    }
}

QJsonObject VisualRuntimeState::ToJson(const RuntimeEvent& event) const
{
    QJsonObject obj;
    obj.insert("type", event.type);
    obj.insert("version", event.version);
    obj.insert("eventId", event.event_id);
    obj.insert("timestamp", event.timestamp);
    obj.insert("source", event.source);
    obj.insert("target", event.target);
    obj.insert("priority", event.priority);
    obj.insert("ttlMs", event.ttl_ms);
    obj.insert("minDisplayMs", event.min_display_ms);
    obj.insert("replaceKey", event.replace_key);
    obj.insert("cooldownKey", event.cooldown_key);
    obj.insert("interruptible", event.interruptible);
    obj.insert("activatedAt", event.activated_at.isValid() ? event.activated_at.toString(Qt::ISODateWithMs) : QString());
    obj.insert("expiresAt", event.ExpireAt().toString(Qt::ISODateWithMs));
    obj.insert("payload", event.payload);
    return obj;
}

bool VisualRuntimeState::HigherPriority(const RuntimeEvent& left, const RuntimeEvent& right)
{
    if (left.priority == right.priority) {
        return left.queued_at < right.queued_at;
    }
    return left.priority > right.priority;
}
