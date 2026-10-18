#pragma once

#include <windows.h>
#include <vector>

std::vector<float> ConvertAudio(const BYTE* data, size_t frames, int channels, bool is_float);
