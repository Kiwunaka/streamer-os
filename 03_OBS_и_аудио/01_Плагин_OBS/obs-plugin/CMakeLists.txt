cmake_minimum_required(VERSION 3.20)
project(audio_visualizer_plugin VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(STREAMER_OS_LOCAL_OBS_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../obs-studio" CACHE PATH "Path to a local OBS Studio source/build tree")
set(STREAMER_OS_LOCAL_OBS_BUILD "${STREAMER_OS_LOCAL_OBS_ROOT}/build_x64" CACHE PATH "Path to the local OBS Studio build tree")

if(EXISTS "${STREAMER_OS_LOCAL_OBS_ROOT}" AND EXISTS "${STREAMER_OS_LOCAL_OBS_BUILD}")
    set(STREAMER_OS_USE_LOCAL_OBS_TREE ON)
else()
    set(STREAMER_OS_USE_LOCAL_OBS_TREE OFF)
endif()

if(STREAMER_OS_USE_LOCAL_OBS_TREE)
    set(_streamer_os_qt_root "${STREAMER_OS_LOCAL_OBS_ROOT}/.deps/obs-deps-qt6-2025-08-23-x64")
    if(EXISTS "${_streamer_os_qt_root}")
        list(APPEND CMAKE_PREFIX_PATH "${_streamer_os_qt_root}")
    endif()
endif()

find_package(Qt6 REQUIRED COMPONENTS Core Widgets Gui Network)

if(STREAMER_OS_USE_LOCAL_OBS_TREE)
    find_path(OBS_CONFIG_INCLUDE_DIR
        NAMES obsconfig.h
        PATHS "${STREAMER_OS_LOCAL_OBS_BUILD}/config"
        NO_DEFAULT_PATH
    )
    find_path(OBS_LIBOBS_INCLUDE_DIR
        NAMES obs-module.h
        PATHS "${STREAMER_OS_LOCAL_OBS_ROOT}/libobs"
        NO_DEFAULT_PATH
    )
    find_path(OBS_FRONTEND_INCLUDE_DIR
        NAMES obs-frontend-api.h
        PATHS "${STREAMER_OS_LOCAL_OBS_ROOT}/frontend/api"
        NO_DEFAULT_PATH
    )

    if(NOT OBS_CONFIG_INCLUDE_DIR OR NOT OBS_LIBOBS_INCLUDE_DIR OR NOT OBS_FRONTEND_INCLUDE_DIR)
        message(FATAL_ERROR "Failed to locate local OBS include directories. Set STREAMER_OS_LOCAL_OBS_ROOT and STREAMER_OS_LOCAL_OBS_BUILD explicitly.")
    endif()

    add_library(OBS::libobs SHARED IMPORTED GLOBAL)
    set_target_properties(OBS::libobs PROPERTIES
        INTERFACE_COMPILE_DEFINITIONS "HAVE_OBSCONFIG_H"
        INTERFACE_INCLUDE_DIRECTORIES "${OBS_CONFIG_INCLUDE_DIR};${OBS_LIBOBS_INCLUDE_DIR}"
        IMPORTED_IMPLIB_DEBUG "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/Debug/obs.lib"
        IMPORTED_IMPLIB_RELEASE "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/Release/obs.lib"
        IMPORTED_IMPLIB_MINSIZEREL "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/MinSizeRel/obs.lib"
        IMPORTED_IMPLIB_RELWITHDEBINFO "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/RelWithDebInfo/obs.lib"
        IMPORTED_LOCATION_DEBUG "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/Debug/obs.dll"
        IMPORTED_LOCATION_RELEASE "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/Release/obs.dll"
        IMPORTED_LOCATION_MINSIZEREL "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/MinSizeRel/obs.dll"
        IMPORTED_LOCATION_RELWITHDEBINFO "${STREAMER_OS_LOCAL_OBS_BUILD}/libobs/RelWithDebInfo/obs.dll"
    )

    add_library(OBS::obs-frontend-api SHARED IMPORTED GLOBAL)
    set_target_properties(OBS::obs-frontend-api PROPERTIES
        INTERFACE_INCLUDE_DIRECTORIES "${OBS_FRONTEND_INCLUDE_DIR}"
        IMPORTED_IMPLIB_DEBUG "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/Debug/obs-frontend-api.lib"
        IMPORTED_IMPLIB_RELEASE "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/Release/obs-frontend-api.lib"
        IMPORTED_IMPLIB_MINSIZEREL "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/MinSizeRel/obs-frontend-api.lib"
        IMPORTED_IMPLIB_RELWITHDEBINFO "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/RelWithDebInfo/obs-frontend-api.lib"
        IMPORTED_LOCATION_DEBUG "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/Debug/obs-frontend-api.dll"
        IMPORTED_LOCATION_RELEASE "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/Release/obs-frontend-api.dll"
        IMPORTED_LOCATION_MINSIZEREL "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/MinSizeRel/obs-frontend-api.dll"
        IMPORTED_LOCATION_RELWITHDEBINFO "${STREAMER_OS_LOCAL_OBS_BUILD}/frontend/api/RelWithDebInfo/obs-frontend-api.dll"
    )
else()
    find_package(libobs REQUIRED)
    find_package(obs-frontend-api REQUIRED)
endif()

add_library(audio-visualizer-plugin MODULE
    src/plugin-main.cpp
    src/audio_visualizer_dialog.cpp
    src/audio_visualizer_dialog_settings.cpp
    src/audio_visualizer_dialog_server_ui.cpp
    src/audio_visualizer_dialog_widget_ui.cpp
    src/audio_visualizer_server.cpp
    src/audio_capture_wasapi.cpp
    src/audio_capture_wasapi_loop.cpp
    src/audio_capture_wasapi_helpers.cpp
    src/fft_analyzer.cpp
    src/http_server.cpp
    src/websocket_server.cpp
    src/track_store.cpp
    src/audio_device_enumerator.cpp
    src/widget_source_manager.cpp
    src/visual_runtime_state.cpp
)

target_include_directories(audio-visualizer-plugin PRIVATE src)

if(STREAMER_OS_USE_LOCAL_OBS_TREE AND NOT OBS_DEPS_ROOT)
    set(OBS_DEPS_ROOT "${STREAMER_OS_LOCAL_OBS_ROOT}/.deps/obs-deps-2025-08-23-x64")
endif()

find_path(OBS_DEPS_INCLUDE
    NAMES websocketpp/config/asio_no_tls.hpp
    HINTS ${OBS_DEPS_ROOT} ${CMAKE_PREFIX_PATH}
    PATH_SUFFIXES include
)

if(NOT OBS_DEPS_INCLUDE)
    message(FATAL_ERROR "obs deps include path not found (websocketpp). Set OBS_DEPS_ROOT or CMAKE_PREFIX_PATH.")
endif()

target_include_directories(audio-visualizer-plugin PRIVATE ${OBS_DEPS_INCLUDE})
target_compile_definitions(audio-visualizer-plugin PRIVATE ASIO_STANDALONE)

target_link_libraries(audio-visualizer-plugin
    PRIVATE
        Qt6::Core
        Qt6::Gui
        Qt6::Widgets
        Qt6::Network
        OBS::libobs
        OBS::obs-frontend-api
        Ole32
        Uuid
        Mmdevapi
        Avrt
)

set_target_properties(audio-visualizer-plugin PROPERTIES
    PREFIX ""
    OUTPUT_NAME "audio-visualizer-plugin"
)

if(CMAKE_CONFIGURATION_TYPES)
    foreach(CONFIG ${CMAKE_CONFIGURATION_TYPES})
        string(TOUPPER "${CONFIG}" CONFIG_UPPER)
        set_target_properties(audio-visualizer-plugin PROPERTIES
            RUNTIME_OUTPUT_DIRECTORY_${CONFIG_UPPER} "${CMAKE_BINARY_DIR}/bin/64bit"
            LIBRARY_OUTPUT_DIRECTORY_${CONFIG_UPPER} "${CMAKE_BINARY_DIR}/bin/64bit"
        )
    endforeach()
else()
    set_target_properties(audio-visualizer-plugin PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/64bit"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/64bit"
    )
endif()
