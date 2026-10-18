#include "audio_device_enumerator.h"

#include <windows.h>
#include <mmdeviceapi.h>
#include <functiondiscoverykeys_devpkey.h>
#include <propvarutil.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

namespace {
struct ScopedCom {
    HRESULT result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    bool should_uninit = SUCCEEDED(result);

    ~ScopedCom() {
        if (should_uninit) {
            CoUninitialize();
        }
    }

    bool ok() const {
        return SUCCEEDED(result) || result == RPC_E_CHANGED_MODE;
    }
};
} // namespace

std::vector<AudioDevice> EnumerateOutputDevices() {
    ScopedCom com;
    std::vector<AudioDevice> devices;
    if (!com.ok()) {
        return devices;
    }

    ComPtr<IMMDeviceEnumerator> enumerator;
    HRESULT hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                                  IID_PPV_ARGS(&enumerator));
    if (FAILED(hr)) {
        return devices;
    }

    ComPtr<IMMDeviceCollection> collection;
    hr = enumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &collection);
    if (FAILED(hr)) {
        return devices;
    }

    UINT count = 0;
    collection->GetCount(&count);
    for (UINT i = 0; i < count; ++i) {
        ComPtr<IMMDevice> device;
        if (FAILED(collection->Item(i, &device))) {
            continue;
        }

        LPWSTR id = nullptr;
        if (FAILED(device->GetId(&id)) || !id) {
            continue;
        }

        ComPtr<IPropertyStore> props;
        if (FAILED(device->OpenPropertyStore(STGM_READ, &props))) {
            CoTaskMemFree(id);
            continue;
        }

        PROPVARIANT name_var;
        PropVariantInit(&name_var);
        std::wstring name;
        if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &name_var)) &&
            name_var.vt == VT_LPWSTR && name_var.pwszVal) {
            name = name_var.pwszVal;
        }
        PropVariantClear(&name_var);

        AudioDevice info;
        info.id = id;
        info.name = name.empty() ? std::wstring(L"Unknown Device") : name;
        devices.push_back(info);

        CoTaskMemFree(id);
    }

    return devices;
}
