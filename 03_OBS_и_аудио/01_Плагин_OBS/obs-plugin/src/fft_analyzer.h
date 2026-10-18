#pragma once

#include <mutex>
#include <vector>

class FftAnalyzer {
public:
    void Configure(int sample_rate, int channels, int bands, double smoothing);
    void PushAudio(const float* interleaved, size_t frames, int channels);
    std::vector<float> GetBands() const;

private:
    void ProcessBlock();

    int sample_rate_ = 44100;
    int channels_ = 2;
    int block_size_ = 2048;
    int bands_ = 128;
    double smoothing_ = 0.5;
    float max_peak_ = 0.01f;

    mutable std::mutex mutex_;
    std::vector<float> buffer_;
    size_t buffer_index_ = 0;
    std::vector<float> smoothed_;
    std::vector<float> last_bands_;
};
