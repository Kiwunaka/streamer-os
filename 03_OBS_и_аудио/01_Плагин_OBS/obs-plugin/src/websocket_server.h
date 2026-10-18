#pragma once

#include <QJsonObject>
#include <QString>
#include <atomic>
#include <mutex>
#include <set>
#include <thread>

#include <websocketpp/config/asio_no_tls.hpp>
#include <websocketpp/server.hpp>

class WebSocketServer {
public:
    WebSocketServer();
    ~WebSocketServer();

    bool Start(uint16_t port, QString* error);
    void Stop();
    void Broadcast(const QJsonObject& payload);
    bool IsRunning() const;

private:
    using Server = websocketpp::server<websocketpp::config::asio>;
    using ConnectionHandle = websocketpp::connection_hdl;

    void HandleOpen(ConnectionHandle hdl);
    void HandleClose(ConnectionHandle hdl);

    Server server_;
    std::thread thread_;
    std::mutex mutex_;
    std::set<ConnectionHandle, std::owner_less<ConnectionHandle>> connections_;
    std::atomic<bool> running_{false};
};
