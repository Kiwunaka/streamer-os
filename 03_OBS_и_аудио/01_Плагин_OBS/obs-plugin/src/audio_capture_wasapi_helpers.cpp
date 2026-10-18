#include "audio_capture_wasapi_helpers.h"

#include <cstdint>

std::vector<float> ConvertAudio(const BYTE* data, size_t frames, int channels, bool is_float) {
    const size_t total = frames * static_cast<size_t>(channels);
    std::vector<float> output(total, 0.0f);
    if (is_float) {
        const float* src = reinterpret_cast<const float*>(data);
        for (size_t i = 0; i < total; ++i) {
            output[i] = src[i];
        }
        return output;
    }

    const int16_t* src = reinterpret_cast<const int16_t*>(data);
    for (size_t i = 0; i < total; ++i) {
        output[i] = static_cast<float>(src[i]) / 32768.0f;
    }
    return output;
}
