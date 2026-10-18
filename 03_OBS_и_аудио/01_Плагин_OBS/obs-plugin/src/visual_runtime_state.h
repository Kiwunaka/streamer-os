#pragma once

#include <QDateTime>
#include <QHash>
#include <QJsonObject>
#include <QList>
#include <QString>

class VisualRuntimeState {
public:
    bool EnqueueEnvelope(const QJsonObject& envelope, QString* error);
    QJsonObject Snapshot();
    void Clear();

private:
    struct RuntimeEvent {
        QString type;
        int version = 1;
        QString event_id;
        QString timestamp;
        QString source;
        QString target;
        int priority = 50;
        int ttl_ms = 8000;
        int min_display_ms = 1200;
        bool interruptible = true;
        QString replace_key;
        QString cooldown_key;
        int cooldown_ms = 0;
        QJsonObject payload;
        QDateTime queued_at;
        QDateTime activated_at;

        bool IsValid() const { return !type.isEmpty() && !event_id.isEmpty(); }
        QDateTime ExpireAt() const;
    };

private:
    bool ParseEnvelope(const QJsonObject& envelope, RuntimeEvent* out, QString* error) const;
    void PromoteIfNeeded();
    QJsonObject ToJson(const RuntimeEvent& event) const;
    static bool HigherPriority(const RuntimeEvent& left, const RuntimeEvent& right);

    RuntimeEvent active_;
    QList<RuntimeEvent> pending_;
    QHash<QString, QDateTime> cooldowns_;
};
