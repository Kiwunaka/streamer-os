#pragma once

#include <QJsonObject>
#include <QMutex>

class TrackStore {
public:
    TrackStore();
    QJsonObject Get() const;
    void UpdateFromJson(const QJsonObject& payload);

private:
    mutable QMutex mutex_;
    QJsonObject track_;
};
