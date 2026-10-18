#include "audio_visualizer_dialog.h"

#include <obs-module.h>
#include <QJsonObject>
#include <QComboBox>
#include <QDateTimeEdit>
#include <QDoubleSpinBox>
#include <QFileInfo>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QSpinBox>
#include <QTimer>

namespace {
QString ModuleDataDir() {
    const char* module_file = obs_module_file("widget_premium.html");
    if (!module_file) {
        return QString();
    }
    return QFileInfo(QString::fromUtf8(module_file)).absolutePath();
}

} // namespace

AudioVisualizerDialog::AudioVisualizerDialog(QWidget* parent)
    : QDialog(parent),
      data_dir_(ModuleDataDir()),
      source_manager_(data_dir_) {
    setWindowTitle("Audio Visualizer");
    setMinimumSize(720, 520);
    BuildUi();
    LoadSettings();

    status_timer_ = new QTimer(this);
    connect(status_timer_, &QTimer::timeout, this, &AudioVisualizerDialog::UpdateServerStatus);
    status_timer_->start(1000);
    UpdateServerStatus();
}

AudioVisualizerDialog::~AudioVisualizerDialog() {
    server_.Stop();
    SaveSettings();
}

void AudioVisualizerDialog::RefreshDevices() {
    device_combo_->clear();

    const auto devices = EnumerateOutputDevices();
    if (devices.empty()) {
        device_combo_->addItem("No devices found", "");
        return;
    }

    for (const auto& device : devices) {
        const QString name = QString::fromWCharArray(device.name.c_str());
        const QString id = QString::fromWCharArray(device.id.c_str());
        device_combo_->addItem(name, id);
    }
}

void AudioVisualizerDialog::UpdateServerStatus() {
    const bool running = server_.IsRunning();
    server_status_->setText(running ? "Status: Running" : "Status: Stopped");
    start_button_->setEnabled(!running);
    stop_button_->setEnabled(running);
    restart_button_->setEnabled(running);
}

void AudioVisualizerDialog::ApplyOverlay(bool create_if_missing) {
    source_manager_.EnsureBrowserSource(BuildOverlayTarget(), BuildWidgetConfig(), create_if_missing);
}

void AudioVisualizerDialog::ApplyWidget(bool create_if_missing) {
    source_manager_.EnsureBrowserSource(BuildWidgetTarget(), BuildWidgetConfig(), create_if_missing);
}

void AudioVisualizerDialog::ApplyRuntime(bool create_if_missing) {
    source_manager_.EnsureBrowserSource(BuildRuntimeTarget(), BuildWidgetConfig(), create_if_missing);
}

AudioServerSettings AudioVisualizerDialog::BuildServerSettings() const {
    AudioServerSettings settings;
    settings.device_id = device_combo_->currentData().toString();
    settings.device_name = device_combo_->currentText();
    settings.bands = bands_combo_->currentData().toInt();
    settings.smoothing = smoothing_spin_->value();
    return settings;
}

WidgetConfig AudioVisualizerDialog::BuildWidgetConfig() const {
    WidgetConfig config;
    config.preset = preset_combo_->currentText();
    config.opacity = opacity_spin_->value();
    config.sensitivity = sensitivity_spin_->value();
    config.glow = glow_spin_->value();
    config.theme = theme_combo_ ? theme_combo_->currentData().toString() : QString();
    config.visualizer = visualizer_combo_ ? visualizer_combo_->currentData().toString() : QString();
    config.accent = accent_edit_ ? accent_edit_->text().trimmed() : QString();
    config.motion = motion_spin_ ? motion_spin_->value() : 1.0;
    if (timer_mode_combo_) {
        config.timer_mode = timer_mode_combo_->currentData().toString();
    }
    if (timer_minutes_spin_) {
        config.timer_minutes = timer_minutes_spin_->value();
    }
    if (timer_start_edit_) {
        config.timer_start = timer_start_edit_->dateTime().toSecsSinceEpoch();
    }
    if (timer_time_edit_) {
        config.timer_time = timer_time_edit_->text().trimmed();
    }
    if (timer_title_edit_) {
        config.timer_title = timer_title_edit_->text().trimmed();
    }
    if (timer_subtitle_edit_) {
        config.timer_subtitle = timer_subtitle_edit_->text().trimmed();
    }
    if (timer_label_edit_) {
        config.timer_label = timer_label_edit_->text().trimmed();
    }
    return config;
}

WidgetTarget AudioVisualizerDialog::BuildOverlayTarget() const {
    WidgetTarget target;
    target.source_name = overlay_name_->text();
    target.file_name = "stream_overlay_premium.html";
    target.width = overlay_width_->value();
    target.height = overlay_height_->value();
    return target;
}

WidgetTarget AudioVisualizerDialog::BuildWidgetTarget() const {
    WidgetTarget target;
    target.source_name = widget_name_->text();
    target.file_name = "widget_premium.html";
    target.width = widget_width_->value();
    target.height = widget_height_->value();
    return target;
}

WidgetTarget AudioVisualizerDialog::BuildRuntimeTarget() const {
    WidgetTarget target;
    target.source_name = runtime_name_->text();
    target.file_name = "visual_runtime.html";
    target.width = runtime_width_->value();
    target.height = runtime_height_->value();
    return target;
}
