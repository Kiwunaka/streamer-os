#include "audio_visualizer_server.h"

#include "audio_capture_wasapi.h"
#include "fft_analyzer.h"
#include "http_server.h"
#include "track_store.h"
#include "visual_runtime_state.h"
#include "websocket_server.h"

#include <QJsonArray>
#include <QJsonObject>
#include <QTimer>
#include <QDebug>
#include <string>

struct AudioVisualizerServer::Impl {
    TrackStore track_store;
    VisualRuntimeState runtime_state;
    HttpServer http_server;
    WebSocketServer websocket_server;
    FftAnalyzer analyzer;
    AudioCaptureWASAPI capture;
    QTimer* timer = nullptr;
    bool running = false;

    explicit Impl(const QString& data_dir) : http_server(&track_store, &runtime_state, data_dir) {}

    void Broadcast() {
        const std::vector<float> bands = analyzer.GetBands();
        if (bands.empty()) {
            return;
        }

        QJsonArray arr;
        float peak = 0.0f;
        for (float v : bands) {
            arr.append(v);
            if (v > peak) {
                peak = v;
            }
        }

        QJsonObject payload;
        payload.insert("type", "fft");
        payload.insert("bands", arr);
        payload.insert("peak", peak);
        websocket_server.Broadcast(payload);
    }
};

AudioVisualizerServer::AudioVisualizerServer(const QString& data_dir)
    : impl_(new Impl(data_dir)) {}

AudioVisualizerServer::~AudioVisualizerServer() {
    Stop();
    delete impl_;
    impl_ = nullptr;
}

bool AudioVisualizerServer::Start(const AudioServerSettings& settings, QString* error) {
    if (!impl_) {
        return false;
    }
    if (impl_->running) {
        Stop();
    }

    impl_->analyzer.Configure(44100, 2, settings.bands, settings.smoothing);

    int sample_rate = 44100;
    int channels = 2;
    std::wstring capture_error;
    const bool capture_ok = impl_->capture.Start(settings.device_id.toStdWString(),
        [this](const float* data, size_t frames, int channels) {
            if (impl_) {
                impl_->analyzer.PushAudio(data, frames, channels);
            }
        },
        &sample_rate, &channels, &capture_error);

    if (!capture_ok) {
        if (error) {
            const std::wstring err = capture_error.empty() ? impl_->capture.LastError() : capture_error;
            *error = QString::fromWCharArray(err.c_str());
        }
        impl_->capture.Stop();
        qWarning() << "[Streamer OS] Audio capture unavailable:" << QString::fromStdWString(capture_error);
        // Keep the settings panel and visual queue available while the device is disconnected.
    }

    if (capture_ok) impl_->analyzer.Configure(sample_rate, channels, settings.bands, settings.smoothing);

    if (!impl_->http_server.Start(settings.http_port, error)) {
        impl_->capture.Stop();
        return false;
    }

    if (!impl_->websocket_server.Start(static_cast<quint16>(settings.websocket_port), error)) {
        impl_->http_server.Stop();
        impl_->capture.Stop();
        return false;
    }

    impl_->timer = new QTimer();
    impl_->timer->setInterval(16);
    QObject::connect(impl_->timer, &QTimer::timeout, impl_->timer, [this]() {
        if (impl_ && impl_->running) {
            impl_->Broadcast();
        }
    });
    impl_->timer->start();

    impl_->running = true;
    return true;
}

void AudioVisualizerServer::Stop() {
    if (!impl_ || !impl_->running) {
        return;
    }

    if (impl_->timer) {
    impl_->timer->stop();
    delete impl_->timer;
    impl_->timer = nullptr;
    }
    impl_->websocket_server.Stop();
    impl_->http_server.Stop();
    impl_->capture.Stop();
    impl_->running = false;
}

bool AudioVisualizerServer::IsRunning() const {
    return impl_ && impl_->running;
}

bool AudioVisualizerServer::SubmitRuntimeEvent(const QJsonObject& envelope, QString* error)
{
    if (!impl_) {
        if (error) {
            *error = "Runtime not initialized.";
        }
        return false;
    }
    return impl_->runtime_state.EnqueueEnvelope(envelope, error);
}

QJsonObject AudioVisualizerServer::RuntimeSnapshot()
{
    if (!impl_) {
        return QJsonObject();
    }
    return impl_->runtime_state.Snapshot();
}

void AudioVisualizerServer::ClearRuntime()
{
    if (!impl_) {
        return;
    }
    impl_->runtime_state.Clear();
}
