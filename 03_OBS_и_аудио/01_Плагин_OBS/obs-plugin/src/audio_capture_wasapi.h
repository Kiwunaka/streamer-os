#pragma once

#include <atomic>
#include <future>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

class AudioCaptureWASAPI {
public:
    using SamplesCallback = std::function<void(const float* data, size_t frames, int channels)>;

    AudioCaptureWASAPI();
    ~AudioCaptureWASAPI();

    bool Start(const std::wstring& device_id, SamplesCallback callback, int* sample_rate, int* channels,
               std::wstring* error);
    void Stop();
    bool IsRunning() const;
    std::wstring LastError() const;

private:
    void CaptureLoop(const std::shared_ptr<std::promise<bool>>& ready);

    SamplesCallback callback_;
    std::wstring device_id_;
    std::thread worker_;
    std::atomic<bool> running_{false};
    void* stop_event_ = nullptr;
    int sample_rate_ = 44100;
    int channels_ = 2;
    bool is_float_ = true;
    mutable std::mutex error_mutex_;
    std::wstring last_error_;
};
