#include <obs-module.h>
#include <obs-frontend-api.h>
#include <QAction>
#include <QWidget>

#include "audio_visualizer_dialog.h"

OBS_DECLARE_MODULE()
OBS_MODULE_USE_DEFAULT_LOCALE("audio-visualizer-plugin", "en-US")

static QAction* g_action = nullptr;
static AudioVisualizerDialog* g_dialog = nullptr;
static const char* kDockId = "streamer_os_runtime_dock";

const char* obs_module_description(void) {
    return "Streamer OS visual runtime and audio visualizer control panel.";
}

bool obs_module_load(void) {
    auto* parent = static_cast<QWidget*>(obs_frontend_get_main_window());
    g_dialog = new AudioVisualizerDialog(parent);
    if (!obs_frontend_add_dock_by_id(kDockId, "Streamer OS Runtime", g_dialog)) {
        delete g_dialog;
        g_dialog = nullptr;
        return false;
    }
    QObject::connect(g_dialog, &QObject::destroyed, []() { g_dialog = nullptr; });

    g_action = static_cast<QAction*>(obs_frontend_add_tools_menu_qaction("Streamer OS Runtime"));
    QObject::connect(g_action, &QAction::triggered, []() {
        if (!g_dialog) {
            return;
        }
        g_dialog->show();
        g_dialog->raise();
        g_dialog->activateWindow();
    });
    return true;
}

void obs_module_unload(void) {
    obs_frontend_remove_dock(kDockId);
    // OBS owns the dock and deletes its child widget during remove_dock.
    g_dialog = nullptr;
}
