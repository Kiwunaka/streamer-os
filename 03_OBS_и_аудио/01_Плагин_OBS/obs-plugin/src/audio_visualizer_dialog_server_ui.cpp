#include "audio_visualizer_dialog.h"

#include <QComboBox>
#include <QDoubleSpinBox>
#include <QFormLayout>
#include <QGroupBox>
#include <QHBoxLayout>
#include <QLabel>
#include <QMessageBox>
#include <QPushButton>
#include <QTabWidget>
#include <QVBoxLayout>

void AudioVisualizerDialog::BuildUi() {
    tabs_ = new QTabWidget(this);
    tabs_->addTab(BuildServerTab(), "Server");
    tabs_->addTab(BuildWidgetTab(), "Widgets");
    tabs_->addTab(BuildRuntimeTab(), "Runtime");

    auto* layout = new QVBoxLayout(this);
    layout->addWidget(tabs_);
}

QWidget* AudioVisualizerDialog::BuildServerTab() {
    auto* tab = new QWidget(this);
    auto* layout = new QVBoxLayout(tab);

    auto* group = new QGroupBox("Server Control", tab);
    auto* form = new QFormLayout(group);

    server_status_ = new QLabel("Status: Unknown", group);
    form->addRow("Status", server_status_);

    device_combo_ = new QComboBox(group);
    RefreshDevices();
    form->addRow("Output device", device_combo_);

    bands_combo_ = new QComboBox(group);
    bands_combo_->addItem("32", 32);
    bands_combo_->addItem("64", 64);
    bands_combo_->addItem("128", 128);
    form->addRow("FFT bands", bands_combo_);

    smoothing_spin_ = new QDoubleSpinBox(group);
    smoothing_spin_->setRange(0.0, 0.9);
    smoothing_spin_->setSingleStep(0.1);
    smoothing_spin_->setDecimals(1);
    form->addRow("Smoothing", smoothing_spin_);

    auto* button_row = new QHBoxLayout();
    start_button_ = new QPushButton("Start", group);
    stop_button_ = new QPushButton("Stop", group);
    restart_button_ = new QPushButton("Restart", group);
    button_row->addWidget(start_button_);
    button_row->addWidget(stop_button_);
    button_row->addWidget(restart_button_);
    form->addRow(button_row);

    layout->addWidget(group);
    layout->addStretch(1);

    connect(start_button_, &QPushButton::clicked, this, [this]() {
        StartServer();
    });
    connect(stop_button_, &QPushButton::clicked, this, [this]() {
        server_.Stop();
        UpdateServerStatus();
    });
    connect(restart_button_, &QPushButton::clicked, this, [this]() {
        RestartServer();
    });

    connect(device_combo_, &QComboBox::currentIndexChanged, this, [this]() {
        SaveSettings();
        if (server_.IsRunning()) {
            RestartServer();
        }
    });
    connect(bands_combo_, &QComboBox::currentIndexChanged, this, [this]() {
        SaveSettings();
        if (server_.IsRunning()) {
            RestartServer();
        }
    });
    connect(smoothing_spin_, &QDoubleSpinBox::valueChanged, this, [this]() {
        SaveSettings();
        if (server_.IsRunning()) {
            RestartServer();
        }
    });

    return tab;
}

void AudioVisualizerDialog::StartServer() {
    QString error;
    if (!server_.Start(BuildServerSettings(), &error)) {
        const QString message = error.isEmpty()
            ? "Failed to start server."
            : "Failed to start server: " + error;
        QMessageBox::warning(this, "Audio Visualizer", message);
    }
    UpdateServerStatus();
}

void AudioVisualizerDialog::RestartServer() {
    server_.Stop();
    StartServer();
}
