#include "audio_visualizer_dialog.h"

#include <obs-frontend-api.h>
#include <util/config-file.h>
#include <QCheckBox>
#include <QComboBox>
#include <QDateTime>
#include <QDateTimeEdit>
#include <QDoubleSpinBox>
#include <QLineEdit>
#include <QSpinBox>

namespace {
constexpr const char* kConfigSection = "AudioVisualizer";

void SetComboValue(QComboBox* combo, const QString& value) {
    if (!combo) {
        return;
    }
    const int index = combo->findText(value);
    if (index >= 0) {
        combo->setCurrentIndex(index);
    }
}
} // namespace

void AudioVisualizerDialog::LoadSettings() {
    config_t* config = obs_frontend_get_profile_config();
    if (!config) {
        return;
    }

    if (config_has_user_value(config, kConfigSection, "Preset")) {
        SetComboValue(preset_combo_, QString::fromUtf8(config_get_string(config, kConfigSection, "Preset")));
    }
    if (config_has_user_value(config, kConfigSection, "Opacity")) {
        opacity_spin_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "Opacity")));
    }
    if (config_has_user_value(config, kConfigSection, "Sensitivity")) {
        sensitivity_spin_->setValue(config_get_double(config, kConfigSection, "Sensitivity"));
    }
    if (config_has_user_value(config, kConfigSection, "Glow")) {
        glow_spin_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "Glow")));
    }
    if (config_has_user_value(config, kConfigSection, "Theme")) {
        const QString theme = QString::fromUtf8(config_get_string(config, kConfigSection, "Theme"));
        const int index = theme_combo_->findData(theme);
        if (index >= 0) {
            theme_combo_->setCurrentIndex(index);
        }
    }
    if (config_has_user_value(config, kConfigSection, "Visualizer")) {
        const QString visualizer = QString::fromUtf8(config_get_string(config, kConfigSection, "Visualizer"));
        const int index = visualizer_combo_->findData(visualizer);
        if (index >= 0) {
            visualizer_combo_->setCurrentIndex(index);
        }
    }
    if (config_has_user_value(config, kConfigSection, "Accent")) {
        accent_edit_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "Accent")));
    }
    if (config_has_user_value(config, kConfigSection, "Motion")) {
        motion_spin_->setValue(config_get_double(config, kConfigSection, "Motion"));
    }
    if (config_has_user_value(config, kConfigSection, "TimerMode")) {
        const QString timer_mode = QString::fromUtf8(config_get_string(config, kConfigSection, "TimerMode"));
        const int index = timer_mode_combo_->findData(timer_mode);
        if (index >= 0) {
            timer_mode_combo_->setCurrentIndex(index);
        }
    }
    if (config_has_user_value(config, kConfigSection, "TimerMinutes")) {
        timer_minutes_spin_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "TimerMinutes")));
    }
    if (config_has_user_value(config, kConfigSection, "TimerStart")) {
        const qint64 seconds = config_get_int(config, kConfigSection, "TimerStart");
        if (seconds > 0) {
            timer_start_edit_->setDateTime(QDateTime::fromSecsSinceEpoch(seconds));
        }
    }
    if (config_has_user_value(config, kConfigSection, "TimerTime")) {
        timer_time_edit_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "TimerTime")));
    }
    if (config_has_user_value(config, kConfigSection, "TimerTitle")) {
        timer_title_edit_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "TimerTitle")));
    }
    if (config_has_user_value(config, kConfigSection, "TimerSubtitle")) {
        timer_subtitle_edit_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "TimerSubtitle")));
    }
    if (config_has_user_value(config, kConfigSection, "TimerLabel")) {
        timer_label_edit_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "TimerLabel")));
    }
    if (config_has_user_value(config, kConfigSection, "OverlayName")) {
        overlay_name_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "OverlayName")));
    }
    if (config_has_user_value(config, kConfigSection, "WidgetName")) {
        widget_name_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "WidgetName")));
    }
    if (config_has_user_value(config, kConfigSection, "OverlayWidth")) {
        overlay_width_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "OverlayWidth")));
    }
    if (config_has_user_value(config, kConfigSection, "OverlayHeight")) {
        overlay_height_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "OverlayHeight")));
    }
    if (config_has_user_value(config, kConfigSection, "WidgetWidth")) {
        widget_width_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "WidgetWidth")));
    }
    if (config_has_user_value(config, kConfigSection, "WidgetHeight")) {
        widget_height_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "WidgetHeight")));
    }
    if (config_has_user_value(config, kConfigSection, "RuntimeName")) {
        runtime_name_->setText(QString::fromUtf8(config_get_string(config, kConfigSection, "RuntimeName")));
    }
    if (config_has_user_value(config, kConfigSection, "RuntimeWidth")) {
        runtime_width_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "RuntimeWidth")));
    }
    if (config_has_user_value(config, kConfigSection, "RuntimeHeight")) {
        runtime_height_->setValue(static_cast<int>(config_get_int(config, kConfigSection, "RuntimeHeight")));
    }
    if (config_has_user_value(config, kConfigSection, "LiveUpdate")) {
        live_update_->setChecked(config_get_bool(config, kConfigSection, "LiveUpdate"));
    }
    if (config_has_user_value(config, kConfigSection, "Bands")) {
        const int bands = static_cast<int>(config_get_int(config, kConfigSection, "Bands"));
        const int index = bands_combo_->findData(bands);
        if (index >= 0) {
            bands_combo_->setCurrentIndex(index);
        }
    }
    if (config_has_user_value(config, kConfigSection, "Smoothing")) {
        smoothing_spin_->setValue(config_get_double(config, kConfigSection, "Smoothing"));
    }

    bool device_selected = false;
    if (config_has_user_value(config, kConfigSection, "DeviceId")) {
        const QString device_id = QString::fromUtf8(config_get_string(config, kConfigSection, "DeviceId"));
        const int index = device_combo_->findData(device_id);
        if (index >= 0) {
            device_combo_->setCurrentIndex(index);
            device_selected = true;
        }
    }
    if (!device_selected && config_has_user_value(config, kConfigSection, "DeviceName")) {
        SetComboValue(device_combo_, QString::fromUtf8(config_get_string(config, kConfigSection, "DeviceName")));
    }
}

void AudioVisualizerDialog::SaveSettings() const {
    config_t* config = obs_frontend_get_profile_config();
    if (!config) {
        return;
    }

    config_set_string(config, kConfigSection, "Preset", preset_combo_->currentText().toUtf8().constData());
    config_set_int(config, kConfigSection, "Opacity", opacity_spin_->value());
    config_set_double(config, kConfigSection, "Sensitivity", sensitivity_spin_->value());
    config_set_int(config, kConfigSection, "Glow", glow_spin_->value());
    config_set_string(config, kConfigSection, "Theme", theme_combo_->currentData().toString().toUtf8().constData());
    config_set_string(config, kConfigSection, "Visualizer", visualizer_combo_->currentData().toString().toUtf8().constData());
    config_set_string(config, kConfigSection, "Accent", accent_edit_->text().toUtf8().constData());
    config_set_double(config, kConfigSection, "Motion", motion_spin_->value());
    config_set_string(config, kConfigSection, "TimerMode", timer_mode_combo_->currentData().toString().toUtf8().constData());
    config_set_int(config, kConfigSection, "TimerMinutes", timer_minutes_spin_->value());
    config_set_int(config, kConfigSection, "TimerStart", static_cast<long long>(timer_start_edit_->dateTime().toSecsSinceEpoch()));
    config_set_string(config, kConfigSection, "TimerTime", timer_time_edit_->text().toUtf8().constData());
    config_set_string(config, kConfigSection, "TimerTitle", timer_title_edit_->text().toUtf8().constData());
    config_set_string(config, kConfigSection, "TimerSubtitle", timer_subtitle_edit_->text().toUtf8().constData());
    config_set_string(config, kConfigSection, "TimerLabel", timer_label_edit_->text().toUtf8().constData());
    config_set_string(config, kConfigSection, "OverlayName", overlay_name_->text().toUtf8().constData());
    config_set_string(config, kConfigSection, "WidgetName", widget_name_->text().toUtf8().constData());
    config_set_int(config, kConfigSection, "OverlayWidth", overlay_width_->value());
    config_set_int(config, kConfigSection, "OverlayHeight", overlay_height_->value());
    config_set_int(config, kConfigSection, "WidgetWidth", widget_width_->value());
    config_set_int(config, kConfigSection, "WidgetHeight", widget_height_->value());
    config_set_string(config, kConfigSection, "RuntimeName", runtime_name_->text().toUtf8().constData());
    config_set_int(config, kConfigSection, "RuntimeWidth", runtime_width_->value());
    config_set_int(config, kConfigSection, "RuntimeHeight", runtime_height_->value());
    config_set_bool(config, kConfigSection, "LiveUpdate", live_update_->isChecked());
    config_set_int(config, kConfigSection, "Bands", bands_combo_->currentData().toInt());
    config_set_double(config, kConfigSection, "Smoothing", smoothing_spin_->value());
    config_set_string(config, kConfigSection, "DeviceId", device_combo_->currentData().toString().toUtf8().constData());
    config_set_string(config, kConfigSection, "DeviceName", device_combo_->currentText().toUtf8().constData());

    config_save(config);
}
