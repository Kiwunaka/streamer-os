#pragma once

#include <QObject>
#include <QTcpServer>
#include <QHash>

class TrackStore;
class VisualRuntimeState;

class HttpServer : public QObject {
public:
    explicit HttpServer(TrackStore* store, VisualRuntimeState* runtime, const QString& data_dir = QString(), QObject* parent = nullptr);
    bool Start(quint16 port, QString* error);
    void Stop();
    bool IsRunning() const;

private:
    void OnNewConnection();
    void OnReadyRead();
    void OnDisconnected();

private:
    void HandleRequest(QTcpSocket* socket, const QByteArray& request);
    QByteArray BuildJsonResponse(int status_code, const QByteArray& body) const;

    TrackStore* store_ = nullptr;
    VisualRuntimeState* runtime_ = nullptr;
    QTcpServer* server_ = nullptr;
    QHash<QTcpSocket*, QByteArray> buffers_;
    QString data_dir_;
};
