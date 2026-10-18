#pragma once

#include <string>
#include <vector>

struct AudioDevice {
    std::wstring id;
    std::wstring name;
};

std::vector<AudioDevice> EnumerateOutputDevices();
