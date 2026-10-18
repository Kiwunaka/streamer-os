#include "websocket_server.h"

#include <QJsonDocument>
#include <QJsonObject>
#include <QString>

WebSocketServer::WebSocketServer() {
    server_.clear_access_channels(websocketpp::log::alevel::all);
    server_.clear_error_channels(websocketpp::log::elevel::all);
}

WebSocketServer::~WebSocketServer() {
    Stop();
}

bool WebSocketServer::Start(uint16_t port, QString* error) {
    if (running_) {
        Stop();
    }

    try {
        server_.init_asio();
        server_.set_reuse_addr(true);
        server_.set_open_handler([this](ConnectionHandle hdl) { HandleOpen(hdl); });
        server_.set_close_handler([this](ConnectionHandle hdl) { HandleClose(hdl); });
        server_.set_fail_handler([this](ConnectionHandle hdl) { HandleClose(hdl); });

        server_.listen(port);
        server_.start_accept();
    } catch (const std::exception& ex) {
        if (error) {
            *error = QString::fromUtf8(ex.what());
        }
        return false;
    }

    running_ = true;
    thread_ = std::thread([this]() {
        try {
            server_.run();
        } catch (...) {
        }
    });
    return true;
}

void WebSocketServer::Stop() {
    if (!running_) {
        return;
    }

    running_ = false;
    try {
        server_.stop_listening();
    } catch (...) {
    }

    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& hdl : connections_) {
            websocketpp::lib::error_code ec;
            server_.close(hdl, websocketpp::close::status::going_away, "", ec);
        }
        connections_.clear();
    }

    try {
        server_.stop();
    } catch (...) {
    }

    if (thread_.joinable()) {
        thread_.join();
    }
}

void WebSocketServer::Broadcast(const QJsonObject& payload) {
    if (!running_) {
        return;
    }

    const QJsonDocument doc(payload);
    const std::string message = doc.toJson(QJsonDocument::Compact).toStdString();
    server_.get_io_service().post([this, message]() {
        std::lock_guard<std::mutex> lock(mutex_);
        for (const auto& hdl : connections_) {
            websocketpp::lib::error_code ec;
            server_.send(hdl, message, websocketpp::frame::opcode::text, ec);
        }
    });
}

bool WebSocketServer::IsRunning() const {
    return running_;
}

void WebSocketServer::HandleOpen(ConnectionHandle hdl) {
    std::lock_guard<std::mutex> lock(mutex_);
    connections_.insert(hdl);
}

void WebSocketServer::HandleClose(ConnectionHandle hdl) {
    std::lock_guard<std::mutex> lock(mutex_);
    connections_.erase(hdl);
}
