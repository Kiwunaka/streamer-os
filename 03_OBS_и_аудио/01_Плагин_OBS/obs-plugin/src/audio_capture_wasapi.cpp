#include "audio_capture_wasapi.h"

#include <windows.h>
#include <chrono>

AudioCaptureWASAPI::AudioCaptureWASAPI() = default;

AudioCaptureWASAPI::~AudioCaptureWASAPI() {
    Stop();
}

bool AudioCaptureWASAPI::Start(const std::wstring& device_id, SamplesCallback callback,
                               int* sample_rate, int* channels, std::wstring* error) {
    if (IsRunning()) {
        Stop();
    }

    callback_ = std::move(callback);
    device_id_ = device_id;
    {
        std::lock_guard<std::mutex> lock(error_mutex_);
        last_error_.clear();
    }
    stop_event_ = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!stop_event_) {
        if (error) {
            *error = L"Failed to create stop event.";
        }
        return false;
    }
    running_ = true;

    auto ready = std::make_shared<std::promise<bool>>();
    auto ready_future = ready->get_future();

    worker_ = std::thread([this, ready]() {
        CaptureLoop(ready);
    });

    if (ready_future.wait_for(std::chrono::seconds(2)) == std::future_status::ready) {
        if (!ready_future.get()) {
            if (error) {
                *error = LastError();
            }
            Stop();
            return false;
        }
    }

    if (sample_rate) {
        *sample_rate = sample_rate_;
    }
    if (channels) {
        *channels = channels_;
    }
    return true;
}

void AudioCaptureWASAPI::Stop() {
    if (stop_event_) {
        SetEvent(stop_event_);
    }
    if (worker_.joinable()) {
        worker_.join();
    }
    if (stop_event_) {
        CloseHandle(stop_event_);
        stop_event_ = nullptr;
    }
    running_ = false;
}

bool AudioCaptureWASAPI::IsRunning() const {
    return running_;
}

std::wstring AudioCaptureWASAPI::LastError() const {
    std::lock_guard<std::mutex> lock(error_mutex_);
    return last_error_;
}
