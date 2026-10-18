#include "fft_analyzer.h"

#include <algorithm>
#include <cmath>
#include <complex>

namespace {
constexpr double kPi = 3.14159265358979323846;
void ApplyBlackmanWindow(std::vector<float>& data) {
    const size_t n = data.size();
    for (size_t i = 0; i < n; ++i) {
        const double a0 = 0.42;
        const double a1 = 0.5;
        const double a2 = 0.08;
        const double phase = (2.0 * kPi * i) / (n - 1);
        const double w = a0 - a1 * std::cos(phase) + a2 * std::cos(2.0 * phase);
        data[i] = static_cast<float>(data[i] * w);
    }
}

void FFT(std::vector<std::complex<float>>& data) {
    const size_t n = data.size();
    size_t j = 0;
    for (size_t i = 1; i < n; ++i) {
        size_t bit = n >> 1;
        for (; j & bit; bit >>= 1) {
            j ^= bit;
        }
        j ^= bit;
        if (i < j) {
            std::swap(data[i], data[j]);
        }
    }

    for (size_t len = 2; len <= n; len <<= 1) {
        const float angle = -2.0f * static_cast<float>(kPi) / static_cast<float>(len);
        const std::complex<float> wlen(std::cos(angle), std::sin(angle));
        for (size_t i = 0; i < n; i += len) {
            std::complex<float> w(1.0f, 0.0f);
            for (size_t k = 0; k < len / 2; ++k) {
                const std::complex<float> u = data[i + k];
                const std::complex<float> v = data[i + k + len / 2] * w;
                data[i + k] = u + v;
                data[i + k + len / 2] = u - v;
                w *= wlen;
            }
        }
    }
}

std::vector<float> BuildLogSpace(float start, float end, int count) {
    std::vector<float> values;
    values.reserve(count);
    const float log_start = std::log10(start);
    const float log_end = std::log10(end);
    const float step = (log_end - log_start) / static_cast<float>(count - 1);
    for (int i = 0; i < count; ++i) {
        values.push_back(std::pow(10.0f, log_start + step * i));
    }
    return values;
}
} // namespace

void FftAnalyzer::Configure(int sample_rate, int channels, int bands, double smoothing) {
    std::lock_guard<std::mutex> lock(mutex_);
    sample_rate_ = sample_rate > 0 ? sample_rate : 44100;
    channels_ = std::max(1, channels);
    bands_ = std::clamp(bands, 8, 256);
    smoothing_ = std::clamp(smoothing, 0.0, 0.95);
    buffer_.assign(block_size_, 0.0f);
    buffer_index_ = 0;
    smoothed_.assign(bands_, 0.0f);
    last_bands_.assign(bands_, 0.0f);
    max_peak_ = 0.01f;
}

void FftAnalyzer::PushAudio(const float* interleaved, size_t frames, int channels) {
    if (!interleaved || frames == 0) {
        return;
    }

    std::lock_guard<std::mutex> lock(mutex_);
    const int ch = std::max(1, channels);
    for (size_t i = 0; i < frames; ++i) {
        float sample = 0.0f;
        const size_t base = i * ch;
        for (int c = 0; c < ch; ++c) {
            sample += interleaved[base + c];
        }
        sample /= static_cast<float>(ch);

        buffer_[buffer_index_++] = sample;
        if (buffer_index_ >= buffer_.size()) {
            ProcessBlock();
            buffer_index_ = 0;
        }
    }
}

std::vector<float> FftAnalyzer::GetBands() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return last_bands_;
}

void FftAnalyzer::ProcessBlock() {
    std::vector<float> windowed = buffer_;
    ApplyBlackmanWindow(windowed);

    std::vector<std::complex<float>> data(windowed.size());
    for (size_t i = 0; i < windowed.size(); ++i) {
        data[i] = std::complex<float>(windowed[i], 0.0f);
    }
    FFT(data);

    const float freq_per_bin = static_cast<float>(sample_rate_) / static_cast<float>(block_size_);
    const std::vector<float> log_starts = BuildLogSpace(20.0f, 16000.0f, bands_ + 1);

    std::vector<float> bands(bands_, 0.0f);
    for (int i = 0; i < bands_; ++i) {
        const float start_freq = log_starts[i];
        const float end_freq = log_starts[i + 1];
        int low_bin = static_cast<int>(start_freq / freq_per_bin);
        int high_bin = static_cast<int>(end_freq / freq_per_bin);
        high_bin = std::max(high_bin, low_bin + 1);
        high_bin = std::min<int>(high_bin, static_cast<int>(data.size() / 2));

        float peak = 0.0f;
        for (int bin = low_bin; bin < high_bin; ++bin) {
            const float mag = std::abs(data[bin]);
            if (mag > peak) {
                peak = mag;
            }
        }
        bands[i] = peak;
    }

    for (int i = 0; i < bands_; ++i) {
        const float eq = std::pow(10.0f, (0.5f * i) / static_cast<float>(bands_));
        const float boosted = bands[i] * eq;
        smoothed_[i] = static_cast<float>(smoothed_[i] * smoothing_ + boosted * (1.0 - smoothing_));
    }

    float peak = *std::max_element(smoothed_.begin(), smoothed_.end());
    if (peak > max_peak_) {
        max_peak_ = peak;
    } else {
        max_peak_ *= 0.995f;
    }
    if (max_peak_ < 0.01f) {
        max_peak_ = 0.01f;
    }

    for (int i = 0; i < bands_; ++i) {
        float normalized = smoothed_[i] / max_peak_;
        normalized = std::pow(normalized, 1.2f);
        last_bands_[i] = std::clamp(normalized, 0.0f, 1.0f);
    }
}
