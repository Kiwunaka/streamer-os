#pragma once

#include <QJsonObject>
#include <QString>

struct AudioServerSettings {
    QString device_id;
    QString device_name;
    int bands = 128;
    double smoothing = 0.5;
    int websocket_port = 8765;
    int http_port = 1608;
};

class AudioVisualizerServer {
public:
    explicit AudioVisualizerServer(const QString& data_dir = QString());
    ~AudioVisualizerServer();

    bool Start(const AudioServerSettings& settings, QString* error);
    void Stop();
    bool IsRunning() const;
    bool SubmitRuntimeEvent(const QJsonObject& envelope, QString* error);
    QJsonObject RuntimeSnapshot();
    void ClearRuntime();

private:
    struct Impl;
    Impl* impl_ = nullptr;
};
