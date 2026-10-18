#pragma once

#include <QDialog>
#include "audio_device_enumerator.h"
#include "audio_visualizer_server.h"
#include "widget_source_manager.h"

class QCheckBox;
class QComboBox;
class QDoubleSpinBox;
class QDateTimeEdit;
class QLabel;
class QLineEdit;
class QPushButton;
class QSpinBox;
class QTabWidget;
class QTimer;

class AudioVisualizerDialog : public QDialog {
public:
    explicit AudioVisualizerDialog(QWidget* parent);
    ~AudioVisualizerDialog() override;

private:
    void BuildUi();
    QWidget* BuildServerTab();
    QWidget* BuildWidgetTab();
    QWidget* BuildRuntimeTab();
    void StartServer();
    void RestartServer();

    void LoadSettings();
    void SaveSettings() const;
    void RefreshDevices();
    void UpdateServerStatus();
    void ApplyOverlay(bool create_if_missing);
    void ApplyWidget(bool create_if_missing);
    void ApplyRuntime(bool create_if_missing);

    AudioServerSettings BuildServerSettings() const;
    WidgetConfig BuildWidgetConfig() const;
    WidgetTarget BuildOverlayTarget() const;
    WidgetTarget BuildWidgetTarget() const;
    WidgetTarget BuildRuntimeTarget() const;

    QString data_dir_;
    WidgetSourceManager source_manager_;
    AudioVisualizerServer server_;
    QTimer* status_timer_ = nullptr;

    QTabWidget* tabs_ = nullptr;
    QLabel* server_status_ = nullptr;
    QComboBox* device_combo_ = nullptr;
    QComboBox* bands_combo_ = nullptr;
    QDoubleSpinBox* smoothing_spin_ = nullptr;
    QPushButton* start_button_ = nullptr;
    QPushButton* stop_button_ = nullptr;
    QPushButton* restart_button_ = nullptr;

    QComboBox* preset_combo_ = nullptr;
    QSpinBox* opacity_spin_ = nullptr;
    QDoubleSpinBox* sensitivity_spin_ = nullptr;
    QSpinBox* glow_spin_ = nullptr;
    QComboBox* theme_combo_ = nullptr;
    QComboBox* visualizer_combo_ = nullptr;
    QLineEdit* accent_edit_ = nullptr;
    QDoubleSpinBox* motion_spin_ = nullptr;
    QLineEdit* overlay_name_ = nullptr;
    QLineEdit* widget_name_ = nullptr;
    QSpinBox* overlay_width_ = nullptr;
    QSpinBox* overlay_height_ = nullptr;
    QSpinBox* widget_width_ = nullptr;
    QSpinBox* widget_height_ = nullptr;
    QComboBox* timer_mode_combo_ = nullptr;
    QSpinBox* timer_minutes_spin_ = nullptr;
    QDateTimeEdit* timer_start_edit_ = nullptr;
    QLineEdit* timer_time_edit_ = nullptr;
    QLineEdit* timer_title_edit_ = nullptr;
    QLineEdit* timer_subtitle_edit_ = nullptr;
    QLineEdit* timer_label_edit_ = nullptr;
    QCheckBox* live_update_ = nullptr;
    QPushButton* insert_overlay_ = nullptr;
    QPushButton* insert_widget_ = nullptr;
    QPushButton* update_overlay_ = nullptr;
    QPushButton* update_widget_ = nullptr;
    QLineEdit* runtime_name_ = nullptr;
    QSpinBox* runtime_width_ = nullptr;
    QSpinBox* runtime_height_ = nullptr;
    QPushButton* insert_runtime_ = nullptr;
    QPushButton* update_runtime_ = nullptr;
    QPushButton* test_runtime_toast_ = nullptr;
    QPushButton* test_runtime_persona_ = nullptr;
    QPushButton* clear_runtime_ = nullptr;
};
