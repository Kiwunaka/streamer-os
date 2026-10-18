#include "track_store.h"

#include <QJsonArray>
#include <QMutexLocker>

TrackStore::TrackStore() {
    track_.insert("status", "stopped");
    track_.insert("title", "");
    track_.insert("artists", QJsonArray());
    track_.insert("album", "");
    track_.insert("album_url", "");
    track_.insert("cover", "");
    track_.insert("cover_url", "");
    track_.insert("progress", 0);
    track_.insert("duration", 0);
    track_.insert("progress_percent", 0);
    track_.insert("source", "");
    track_.insert("hostname", "");
    track_.insert("href", "");
    track_.insert("next_track", QJsonObject());
    track_.insert("last_update", 0);
    track_.insert("script_version", "");
}

QJsonObject TrackStore::Get() const {
    QMutexLocker lock(&mutex_);
    return track_;
}

void TrackStore::UpdateFromJson(const QJsonObject& payload) {
    QMutexLocker lock(&mutex_);

    QJsonObject data = payload;
    if (payload.contains("data") && payload.value("data").isObject()) {
        data = payload.value("data").toObject();
    }

    track_.insert("status", data.value("status").toString("playing"));
    track_.insert("title", data.value("title").toString());
    track_.insert("artists", data.value("artists").toArray());
    track_.insert("album", data.value("album").toString());
    track_.insert("album_url", data.value("album_url").toString());
    const QString cover = data.value("cover").toString(data.value("cover_url").toString());
    const QString cover_url = data.value("cover_url").toString(data.value("cover").toString());
    track_.insert("cover", cover);
    track_.insert("cover_url", cover_url);
    track_.insert("progress", data.value("progress").toDouble(0));
    track_.insert("duration", data.value("duration").toDouble(0));
    track_.insert("source", data.value("source").toString());
    track_.insert("hostname", payload.value("hostname").toString(data.value("hostname").toString()));
    track_.insert("href", payload.value("href").toString(data.value("href").toString()));
    track_.insert("next_track", data.value("next_track").toObject());
    track_.insert("last_update", payload.value("date").toVariant().toLongLong());
    track_.insert("script_version", payload.value("version").toString());

    const double duration = track_.value("duration").toDouble(0);
    if (duration > 0.0) {
        const double progress = track_.value("progress").toDouble(0);
        track_.insert("progress_percent", (progress / duration) * 100.0);
    } else {
        track_.insert("progress_percent", 0);
    }
}
