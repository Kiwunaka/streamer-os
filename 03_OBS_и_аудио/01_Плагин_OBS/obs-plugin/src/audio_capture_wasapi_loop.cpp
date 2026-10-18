#include "audio_capture_wasapi.h"

#include <windows.h>
#include <audioclient.h>
#include <mmdeviceapi.h>
#include <wrl/client.h>
#include "audio_capture_wasapi_helpers.h"

using Microsoft::WRL::ComPtr;

namespace {
struct ScopedCom {
    HRESULT result = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    ~ScopedCom() {
        if (SUCCEEDED(result)) {
            CoUninitialize();
        }
    }
    bool ok() const {
        return SUCCEEDED(result) || result == RPC_E_CHANGED_MODE;
    }
};

} // namespace

void AudioCaptureWASAPI::CaptureLoop(const std::shared_ptr<std::promise<bool>>& ready) {
    ScopedCom com;
    if (!com.ok()) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"COM initialization failed.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    ComPtr<IMMDeviceEnumerator> enumerator;
    HRESULT hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                                  IID_PPV_ARGS(&enumerator));
    if (FAILED(hr)) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to create device enumerator.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    ComPtr<IMMDevice> device;
    if (!device_id_.empty()) {
        hr = enumerator->GetDevice(device_id_.c_str(), &device);
    } else {
        hr = enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device);
    }
    if (FAILED(hr)) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to open audio device.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    ComPtr<IAudioClient> audio_client;
    hr = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr,
                          reinterpret_cast<void**>(audio_client.GetAddressOf()));
    if (FAILED(hr)) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to activate audio client.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    WAVEFORMATEX* mix_format = nullptr;
    hr = audio_client->GetMixFormat(&mix_format);
    if (FAILED(hr) || !mix_format) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to get audio format.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    sample_rate_ = static_cast<int>(mix_format->nSamplesPerSec);
    channels_ = static_cast<int>(mix_format->nChannels);
    is_float_ = mix_format->wBitsPerSample == 32;

    const REFERENCE_TIME buffer_duration = 10000000; // 1s
    hr = audio_client->Initialize(AUDCLNT_SHAREMODE_SHARED,
                                  AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
                                  buffer_duration, 0, mix_format, nullptr);
    CoTaskMemFree(mix_format);
    if (FAILED(hr)) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to initialize audio client.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    HANDLE audio_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!audio_event) {
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to create audio event.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    hr = audio_client->SetEventHandle(audio_event);
    if (FAILED(hr)) {
        CloseHandle(audio_event);
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to set event handle.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    ComPtr<IAudioCaptureClient> capture_client;
    hr = audio_client->GetService(__uuidof(IAudioCaptureClient),
                                  reinterpret_cast<void**>(capture_client.GetAddressOf()));
    if (FAILED(hr)) {
        CloseHandle(audio_event);
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to get capture client.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    hr = audio_client->Start();
    if (FAILED(hr)) {
        CloseHandle(audio_event);
        {
            std::lock_guard<std::mutex> lock(error_mutex_);
            last_error_ = L"Failed to start audio client.";
        }
        running_ = false;
        ready->set_value(false);
        return;
    }

    ready->set_value(true);

    HANDLE wait_handles[2] = {stop_event_, audio_event};
    while (running_) {
        const DWORD wait = WaitForMultipleObjects(2, wait_handles, FALSE, 200);
        if (wait == WAIT_OBJECT_0) {
            running_ = false;
            break;
        }
        if (wait != WAIT_OBJECT_0 + 1) {
            continue;
        }

        UINT32 packet_frames = 0;
        hr = capture_client->GetNextPacketSize(&packet_frames);
        while (SUCCEEDED(hr) && packet_frames > 0) {
            BYTE* data = nullptr;
            UINT32 frames = 0;
            DWORD flags = 0;
            hr = capture_client->GetBuffer(&data, &frames, &flags, nullptr, nullptr);
            if (FAILED(hr)) {
                break;
            }

            const bool is_silence = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0;
            if (!is_silence && data && callback_) {
                const auto samples = ConvertAudio(data, frames, channels_, is_float_);
                callback_(samples.data(), frames, channels_);
            }

            capture_client->ReleaseBuffer(frames);
            hr = capture_client->GetNextPacketSize(&packet_frames);
        }
    }

    audio_client->Stop();
    CloseHandle(audio_event);
    running_ = false;
}
