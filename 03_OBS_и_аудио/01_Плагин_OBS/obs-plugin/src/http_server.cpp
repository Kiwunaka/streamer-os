#include "http_server.h"
#include "track_store.h"
#include "visual_runtime_state.h"
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonParseError>
#include <QHostAddress>
#include <QTcpSocket>
#include <QFile>
namespace {
constexpr int kMaxHeaderBytes = 16384;
constexpr int kMaxBodyBytes = 32 * 1024 * 1024; // Base64 image and speech in donation.scene.v1.
int ParseContentLength(const QByteArray& headers) {
    int content_length = 0;
    bool found = false;
    const QList<QByteArray> lines = headers.split('\n');
    for (const QByteArray& line : lines) {
        const QByteArray trimmed = line.trimmed();
        if (trimmed.toLower().startsWith("content-length:")) {
            if (found) return -1;
            found = true;
            const QByteArray value = trimmed.mid(15).trimmed();
            bool ok = false;
            const int length = value.toInt(&ok);
            if (!ok || length < 0) return -1;
            content_length = length;
        }
        if (trimmed.toLower().startsWith("transfer-encoding:")) return -1;
    }
    return content_length;
}

QByteArray StatusText(int code) {
    switch (code) {
        case 200:
            return "OK";
        case 400:
            return "Bad Request";
        case 404:
            return "Not Found";
        case 413:
            return "Payload Too Large";
        case 422:
            return "Unprocessable Entity";
        default:
            return "OK";
    }
}
} // namespace

HttpServer::HttpServer(TrackStore* store, VisualRuntimeState* runtime, const QString& data_dir, QObject* parent)
    : QObject(parent),
      store_(store),
      runtime_(runtime), data_dir_(data_dir) {}

bool HttpServer::Start(quint16 port, QString* error) {
    if (server_) {
        Stop();
    }

    server_ = new QTcpServer(this);
    if (!server_->listen(QHostAddress::LocalHost, port)) {
        if (error) {
            *error = "Failed to start HTTP server.";
        }
        delete server_;
        server_ = nullptr;
        return false;
    }

    connect(server_, &QTcpServer::newConnection, this, &HttpServer::OnNewConnection);
    return true;
}

void HttpServer::Stop() {
    if (!server_) {
        return;
    }

    for (QTcpSocket* socket : buffers_.keys()) {
        if (socket) {
            socket->disconnectFromHost();
            socket->deleteLater();
        }
    }
    buffers_.clear();

    server_->close();
    server_->deleteLater();
    server_ = nullptr;
}

bool HttpServer::IsRunning() const {
    return server_ != nullptr;
}

void HttpServer::OnNewConnection() {
    if (!server_) {
        return;
    }

    while (server_->hasPendingConnections()) {
        QTcpSocket* socket = server_->nextPendingConnection();
        buffers_.insert(socket, QByteArray());
        connect(socket, &QTcpSocket::readyRead, this, &HttpServer::OnReadyRead);
        connect(socket, &QTcpSocket::disconnected, this, &HttpServer::OnDisconnected);
    }
}

void HttpServer::OnReadyRead() {
    QTcpSocket* socket = qobject_cast<QTcpSocket*>(sender());
    if (!socket) {
        return;
    }

    QByteArray& buffer = buffers_[socket];
    buffer.append(socket->readAll());
    if (buffer.size() > kMaxHeaderBytes + kMaxBodyBytes) {
        socket->write(BuildJsonResponse(413, "{}"));
        socket->disconnectFromHost();
        return;
    }

    while (true) {
        const int header_end = buffer.indexOf("\r\n\r\n");
        if (header_end > kMaxHeaderBytes || (header_end < 0 && buffer.size() > kMaxHeaderBytes)) {
            socket->write(BuildJsonResponse(413, "{}"));
            socket->disconnectFromHost();
            return;
        }
        if (header_end < 0) {
            return;
        }

        const QByteArray header = buffer.left(header_end + 4);
        const int content_length = ParseContentLength(header);
        if (content_length < 0 || content_length > kMaxBodyBytes) {
            socket->write(BuildJsonResponse(content_length < 0 ? 400 : 413, "{}"));
            socket->disconnectFromHost();
            return;
        }
        const int total_length = header_end + 4 + content_length;
        if (buffer.size() < total_length) {
            return;
        }

        const QByteArray request = buffer.left(total_length);
        buffer.remove(0, total_length);
        HandleRequest(socket, request);
    }
}

void HttpServer::OnDisconnected() {
    QTcpSocket* socket = qobject_cast<QTcpSocket*>(sender());
    buffers_.remove(socket);
    if (socket) {
        socket->deleteLater();
    }
}

void HttpServer::HandleRequest(QTcpSocket* socket, const QByteArray& request) {
    const int header_end = request.indexOf("\r\n\r\n");
    if (header_end < 0) {
        socket->write(BuildJsonResponse(400, "{}"));
        return;
    }

    const QByteArray header = request.left(header_end);
    const QByteArray body = request.mid(header_end + 4);
    const QList<QByteArray> lines = header.split('\n');
    if (lines.isEmpty()) {
        socket->write(BuildJsonResponse(400, "{}"));
        return;
    }

    const QList<QByteArray> request_line = lines.first().trimmed().split(' ');
    if (request_line.size() < 2) {
        socket->write(BuildJsonResponse(400, "{}"));
        return;
    }

    const QByteArray method = request_line[0].toUpper();
    const QByteArray path = request_line[1];
    if (method == "OPTIONS") {
        socket->write(BuildJsonResponse(200, "{}"));
        return;
    }

    if (method == "GET") {
        if (path == "/admin" || path == "/admin/" || path == "/admin.js" || path == "/admin.css") {
            const QString file_name = (path == "/admin" || path == "/admin/") ? "admin.html" : QString::fromUtf8(path.mid(1));
            QFile file(data_dir_ + "/" + file_name);
            if (!file.open(QIODevice::ReadOnly)) {
                socket->write(BuildJsonResponse(404, "{\"message\":\"Admin files are not installed.\"}"));
                return;
            }
            const QByteArray content_type = file_name.endsWith(".html") ? "text/html; charset=utf-8" :
                (file_name.endsWith(".js") ? "text/javascript; charset=utf-8" : "text/css; charset=utf-8");
            const QByteArray body = file.readAll();
            socket->write("HTTP/1.1 200 OK\r\nContent-Type: " + content_type + "\r\nCache-Control: no-store\r\nContent-Length: " + QByteArray::number(body.size()) + "\r\n\r\n" + body);
            return;
        }
        if (path == "/runtime/state") {
            const QJsonObject snapshot = runtime_ ? runtime_->Snapshot() : QJsonObject();
            socket->write(BuildJsonResponse(200, QJsonDocument(snapshot).toJson(QJsonDocument::Compact)));
            return;
        }

        if (path == "/health") {
            const QJsonObject payload{
                { "status", "ok" },
                { "runtime", runtime_ ? "enabled" : "disabled" }
            };
            socket->write(BuildJsonResponse(200, QJsonDocument(payload).toJson(QJsonDocument::Compact)));
            return;
        }

        const QJsonObject track = store_ ? store_->Get() : QJsonObject();
        const QByteArray json = QJsonDocument(track).toJson(QJsonDocument::Compact);
        socket->write(BuildJsonResponse(200, json));
        return;
    }

    if (method == "POST") {
        QJsonParseError err = {};
        const QJsonDocument doc = QJsonDocument::fromJson(body, &err);
        if (err.error != QJsonParseError::NoError || !doc.isObject()) {
            socket->write(BuildJsonResponse(400, "{\"status\":\"error\"}"));
            return;
        }

        if (path == "/events") {
            QString runtime_error;
            const bool ok = runtime_ && runtime_->EnqueueEnvelope(doc.object(), &runtime_error);
            if (!ok) {
                const QJsonObject payload{
                    { "status", "error" },
                    { "message", runtime_error.isEmpty() ? "runtime rejected event" : runtime_error }
                };
                socket->write(BuildJsonResponse(422, QJsonDocument(payload).toJson(QJsonDocument::Compact)));
                return;
            }

            const QJsonObject payload{
                { "status", "accepted" },
                { "pendingCount", runtime_ ? runtime_->Snapshot().value("pendingCount").toInt() : 0 }
            };
            socket->write(BuildJsonResponse(200, QJsonDocument(payload).toJson(QJsonDocument::Compact)));
            return;
        }

        if (path == "/runtime/clear") {
            if (runtime_) {
                runtime_->Clear();
            }
            socket->write(BuildJsonResponse(200, "{\"status\":\"cleared\"}"));
            return;
        }

        if (store_) {
            store_->UpdateFromJson(doc.object());
        }
        socket->write(BuildJsonResponse(200, "{\"status\":\"ok\"}"));
        return;
    }

    socket->write(BuildJsonResponse(404, "{}"));
}

QByteArray HttpServer::BuildJsonResponse(int status_code, const QByteArray& body) const {
    QByteArray response;
    response += "HTTP/1.1 ";
    response += QByteArray::number(status_code);
    response += " ";
    response += StatusText(status_code);
    response += "\r\n";
    response += "Content-Type: application/json\r\n";
    response += "Access-Control-Allow-Origin: *\r\n";
    response += "Access-Control-Allow-Headers: *\r\n";
    response += "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n";
    response += "Content-Length: ";
    response += QByteArray::number(body.size());
    response += "\r\n\r\n";
    response += body;
    return response;
}
