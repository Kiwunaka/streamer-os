#include "audio_visualizer_dialog.h"

#include <QCheckBox>
#include <QComboBox>
#include <QDateTime>
#include <QDateTimeEdit>
#include <QDoubleSpinBox>
#include <QFormLayout>
#include <QGroupBox>
#include <QHBoxLayout>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QJsonObject>
#include <QSpinBox>
#include <QUuid>
#include <QVBoxLayout>

QWidget* AudioVisualizerDialog::BuildWidgetTab() {
    auto* tab = new QWidget(this);
    auto* layout = new QVBoxLayout(tab);

    auto* notice = new QLabel("IMPORTANT: Install tuna_universal_v7.user.js "
                              "(Tampermonkey/Violentmonkey) or track info will be empty.", tab);
    notice->setWordWrap(true);
    notice->setStyleSheet("color: #d9852c; font-weight: 600;");
    layout->addWidget(notice);

    auto* widget_group = new QGroupBox("Widget Style", tab);
    auto* widget_form = new QFormLayout(widget_group);

    preset_combo_ = new QComboBox(widget_group);
    preset_combo_->addItem("vinyl");
    preset_combo_->addItem("pill");
    preset_combo_->addItem("glass");
    preset_combo_->addItem("minimal");
    preset_combo_->addItem("stack");
    preset_combo_->addItem("poster");
    preset_combo_->addItem("orbit");
    widget_form->addRow("Preset", preset_combo_);

    opacity_spin_ = new QSpinBox(widget_group);
    opacity_spin_->setRange(0, 100);
    opacity_spin_->setSingleStep(5);
    widget_form->addRow("Opacity (%)", opacity_spin_);

    sensitivity_spin_ = new QDoubleSpinBox(widget_group);
    sensitivity_spin_->setRange(0.5, 3.0);
    sensitivity_spin_->setSingleStep(0.1);
    sensitivity_spin_->setDecimals(1);
    widget_form->addRow("Sensitivity", sensitivity_spin_);

    glow_spin_ = new QSpinBox(widget_group);
    glow_spin_->setRange(0, 100);
    glow_spin_->setSingleStep(5);
    widget_form->addRow("Glow (%)", glow_spin_);

    theme_combo_ = new QComboBox(widget_group);
    theme_combo_->addItem("Ember (warm)", "ember");
    theme_combo_->addItem("Ice (cool)", "ice");
    theme_combo_->addItem("Mono (studio)", "mono");
    theme_combo_->addItem("Signal (neon)", "signal");
    widget_form->addRow("Theme", theme_combo_);

    visualizer_combo_ = new QComboBox(widget_group);
    visualizer_combo_->addItem("Radial", "radial");
    visualizer_combo_->addItem("Ring", "ring");
    visualizer_combo_->addItem("Wave", "wave");
    visualizer_combo_->addItem("Hex Tiles", "hex");
    visualizer_combo_->addItem("Tiles", "tiles");
    visualizer_combo_->addItem("Bars", "bars");
    widget_form->addRow("Visualizer", visualizer_combo_);

    accent_edit_ = new QLineEdit(widget_group);
    accent_edit_->setPlaceholderText("#f8b84e");
    widget_form->addRow("Accent (hex)", accent_edit_);

    motion_spin_ = new QDoubleSpinBox(widget_group);
    motion_spin_->setRange(0.5, 2.5);
    motion_spin_->setSingleStep(0.1);
    motion_spin_->setDecimals(1);
    motion_spin_->setValue(1.0);
    widget_form->addRow("Motion", motion_spin_);

    layout->addWidget(widget_group);

    auto* timer_group = new QGroupBox("Overlay Timer", tab);
    auto* timer_form = new QFormLayout(timer_group);

    timer_mode_combo_ = new QComboBox(timer_group);
    timer_mode_combo_->addItem("Auto (default)", "auto");
    timer_mode_combo_->addItem("Off", "off");
    timer_mode_combo_->addItem("Countdown (minutes)", "minutes");
    timer_mode_combo_->addItem("Start time (timestamp)", "start");
    timer_mode_combo_->addItem("Time of day (HH:MM)", "time");
    timer_form->addRow("Mode", timer_mode_combo_);

    timer_minutes_spin_ = new QSpinBox(timer_group);
    timer_minutes_spin_->setRange(1, 240);
    timer_minutes_spin_->setValue(10);
    timer_form->addRow("Minutes", timer_minutes_spin_);

    timer_start_edit_ = new QDateTimeEdit(QDateTime::currentDateTime().addSecs(600), timer_group);
    timer_start_edit_->setDisplayFormat("yyyy-MM-dd HH:mm");
    timer_form->addRow("Start at", timer_start_edit_);

    timer_time_edit_ = new QLineEdit(timer_group);
    timer_time_edit_->setPlaceholderText("19:00");
    timer_form->addRow("Time (HH:MM)", timer_time_edit_);

    timer_title_edit_ = new QLineEdit(timer_group);
    timer_title_edit_->setPlaceholderText("STREAM STARTS");
    timer_form->addRow("Title", timer_title_edit_);

    timer_subtitle_edit_ = new QLineEdit(timer_group);
    timer_subtitle_edit_->setPlaceholderText("Stand by. We go live soon.");
    timer_form->addRow("Subtitle", timer_subtitle_edit_);

    timer_label_edit_ = new QLineEdit(timer_group);
    timer_label_edit_->setPlaceholderText("Starting soon");
    timer_form->addRow("Status label", timer_label_edit_);

    layout->addWidget(timer_group);

    auto* overlay_group = new QGroupBox("Overlay Source", tab);
    auto* overlay_form = new QFormLayout(overlay_group);
    overlay_name_ = new QLineEdit("Audio Visualizer Overlay", overlay_group);
    overlay_width_ = new QSpinBox(overlay_group);
    overlay_height_ = new QSpinBox(overlay_group);
    overlay_width_->setRange(100, 4096);
    overlay_height_->setRange(100, 4096);
    overlay_width_->setValue(1920);
    overlay_height_->setValue(1080);
    overlay_form->addRow("Source name", overlay_name_);
    overlay_form->addRow("Width", overlay_width_);
    overlay_form->addRow("Height", overlay_height_);
    layout->addWidget(overlay_group);

    auto* widget_group2 = new QGroupBox("Widget Source", tab);
    auto* widget_form2 = new QFormLayout(widget_group2);
    widget_name_ = new QLineEdit("Audio Visualizer Widget", widget_group2);
    widget_width_ = new QSpinBox(widget_group2);
    widget_height_ = new QSpinBox(widget_group2);
    widget_width_->setRange(100, 4096);
    widget_height_->setRange(100, 4096);
    widget_width_->setValue(700);
    widget_height_->setValue(300);
    widget_form2->addRow("Source name", widget_name_);
    widget_form2->addRow("Width", widget_width_);
    widget_form2->addRow("Height", widget_height_);
    layout->addWidget(widget_group2);

    live_update_ = new QCheckBox("Live update sources when settings change", tab);
    live_update_->setChecked(true);
    layout->addWidget(live_update_);

    auto* buttons = new QHBoxLayout();
    insert_overlay_ = new QPushButton("Insert overlay", tab);
    update_overlay_ = new QPushButton("Update overlay", tab);
    insert_widget_ = new QPushButton("Insert widget", tab);
    update_widget_ = new QPushButton("Update widget", tab);
    buttons->addWidget(insert_overlay_);
    buttons->addWidget(update_overlay_);
    buttons->addWidget(insert_widget_);
    buttons->addWidget(update_widget_);
    layout->addLayout(buttons);
    layout->addStretch(1);

    auto apply_if_live = [this]() {
        SaveSettings();
        if (live_update_->isChecked()) {
            ApplyOverlay(false);
            ApplyWidget(false);
        }
    };

    connect(preset_combo_, &QComboBox::currentIndexChanged, this, apply_if_live);
    connect(opacity_spin_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(sensitivity_spin_, &QDoubleSpinBox::valueChanged, this, apply_if_live);
    connect(glow_spin_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(theme_combo_, &QComboBox::currentIndexChanged, this, apply_if_live);
    connect(visualizer_combo_, &QComboBox::currentIndexChanged, this, apply_if_live);
    connect(accent_edit_, &QLineEdit::textChanged, this, apply_if_live);
    connect(motion_spin_, &QDoubleSpinBox::valueChanged, this, apply_if_live);
    connect(timer_mode_combo_, &QComboBox::currentIndexChanged, this, apply_if_live);
    connect(timer_minutes_spin_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(timer_start_edit_, &QDateTimeEdit::dateTimeChanged, this, apply_if_live);
    connect(timer_time_edit_, &QLineEdit::textChanged, this, apply_if_live);
    connect(timer_title_edit_, &QLineEdit::textChanged, this, apply_if_live);
    connect(timer_subtitle_edit_, &QLineEdit::textChanged, this, apply_if_live);
    connect(timer_label_edit_, &QLineEdit::textChanged, this, apply_if_live);
    connect(overlay_name_, &QLineEdit::textChanged, this, apply_if_live);
    connect(widget_name_, &QLineEdit::textChanged, this, apply_if_live);
    connect(overlay_width_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(overlay_height_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(widget_width_, &QSpinBox::valueChanged, this, apply_if_live);
    connect(widget_height_, &QSpinBox::valueChanged, this, apply_if_live);

    connect(insert_overlay_, &QPushButton::clicked, this, [this]() {
        ApplyOverlay(true);
    });
    connect(update_overlay_, &QPushButton::clicked, this, [this]() {
        ApplyOverlay(false);
    });
    connect(insert_widget_, &QPushButton::clicked, this, [this]() {
        ApplyWidget(true);
    });
    connect(update_widget_, &QPushButton::clicked, this, [this]() {
        ApplyWidget(false);
    });

    return tab;
}

QWidget* AudioVisualizerDialog::BuildRuntimeTab() {
    auto* tab = new QWidget(this);
    auto* layout = new QVBoxLayout(tab);

    auto* intro = new QLabel(
        "Unified visual runtime for persona switches, recap cards, toasts, and other versioned events. "
        "Insert this source once and let Streamer.bot talk to the plugin through /events.",
        tab);
    intro->setWordWrap(true);
    layout->addWidget(intro);

    auto* source_group = new QGroupBox("Runtime Source", tab);
    auto* source_form = new QFormLayout(source_group);
    runtime_name_ = new QLineEdit("Streamer OS Runtime", source_group);
    runtime_width_ = new QSpinBox(source_group);
    runtime_height_ = new QSpinBox(source_group);
    runtime_width_->setRange(100, 4096);
    runtime_height_->setRange(100, 4096);
    runtime_width_->setValue(1920);
    runtime_height_->setValue(1080);
    source_form->addRow("Source name", runtime_name_);
    source_form->addRow("Width", runtime_width_);
    source_form->addRow("Height", runtime_height_);
    layout->addWidget(source_group);

    auto* endpoint = new QLabel("Ingress endpoints: POST /events, GET /runtime/state, POST /runtime/clear", tab);
    endpoint->setStyleSheet("color: #6d8ca3;");
    endpoint->setWordWrap(true);
    layout->addWidget(endpoint);

    auto apply_runtime_if_live = [this]() {
        SaveSettings();
        if (live_update_ && live_update_->isChecked()) {
            ApplyRuntime(false);
        }
    };

    connect(runtime_name_, &QLineEdit::textChanged, this, apply_runtime_if_live);
    connect(runtime_width_, &QSpinBox::valueChanged, this, apply_runtime_if_live);
    connect(runtime_height_, &QSpinBox::valueChanged, this, apply_runtime_if_live);

    auto* source_buttons = new QHBoxLayout();
    insert_runtime_ = new QPushButton("Insert runtime", tab);
    update_runtime_ = new QPushButton("Update runtime", tab);
    clear_runtime_ = new QPushButton("Clear runtime", tab);
    source_buttons->addWidget(insert_runtime_);
    source_buttons->addWidget(update_runtime_);
    source_buttons->addWidget(clear_runtime_);
    layout->addLayout(source_buttons);

    auto* test_buttons = new QHBoxLayout();
    test_runtime_toast_ = new QPushButton("Test toast", tab);
    test_runtime_persona_ = new QPushButton("Test persona switch", tab);
    test_buttons->addWidget(test_runtime_toast_);
    test_buttons->addWidget(test_runtime_persona_);
    layout->addLayout(test_buttons);
    layout->addStretch(1);

    connect(insert_runtime_, &QPushButton::clicked, this, [this]() {
        ApplyRuntime(true);
    });
    connect(update_runtime_, &QPushButton::clicked, this, [this]() {
        ApplyRuntime(false);
    });
    connect(clear_runtime_, &QPushButton::clicked, this, [this]() {
        server_.ClearRuntime();
    });
    connect(test_runtime_toast_, &QPushButton::clicked, this, [this]() {
        QJsonObject payload;
        payload.insert("message", "Runtime event bridge is alive.");
        payload.insert("level", "info");

        QJsonObject queue;
        queue.insert("interruptible", true);
        queue.insert("replaceByKey", "runtime-toast");
        queue.insert("minDisplayMs", 1400);
        queue.insert("cooldownKey", "runtime-toast");
        queue.insert("cooldownMs", 500);

        QJsonObject envelope;
        envelope.insert("type", "system.toast.v1");
        envelope.insert("version", 1);
        envelope.insert("eventId", QUuid::createUuid().toString(QUuid::WithoutBraces));
        envelope.insert("timestamp", QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs));
        envelope.insert("source", "obs.plugin");
        envelope.insert("target", "runtime");
        envelope.insert("priority", 40);
        envelope.insert("ttlMs", 5000);
        envelope.insert("payload", payload);
        envelope.insert("queue", queue);
        QString error;
        server_.SubmitRuntimeEvent(envelope, &error);
    });
    connect(test_runtime_persona_, &QPushButton::clicked, this, [this]() {
        QJsonObject intro;
        intro.insert("image", "intro_Skuf.png");
        intro.insert("audio", "intro_Skuf.mp3");

        QJsonObject payload;
        payload.insert("persona", "Skuf");
        payload.insert("notification", "Mode: DRUNK SKUF activated!");
        payload.insert("voiceProfile", "D2XHvQ49kMik0wARO3WW");
        payload.insert("themePreset", "skuf-garage");
        payload.insert("imageStyleProfile", "skuf-real");
        payload.insert("intro", intro);

        QJsonObject queue;
        queue.insert("interruptible", true);
        queue.insert("replaceByKey", "persona-switch");
        queue.insert("minDisplayMs", 2600);
        queue.insert("cooldownKey", "persona-switch");
        queue.insert("cooldownMs", 500);

        QJsonObject envelope;
        envelope.insert("type", "persona.switch.v1");
        envelope.insert("version", 1);
        envelope.insert("eventId", QUuid::createUuid().toString(QUuid::WithoutBraces));
        envelope.insert("timestamp", QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs));
        envelope.insert("source", "obs.plugin");
        envelope.insert("target", "runtime");
        envelope.insert("priority", 80);
        envelope.insert("ttlMs", 9000);
        envelope.insert("payload", payload);
        envelope.insert("queue", queue);
        QString error;
        server_.SubmitRuntimeEvent(envelope, &error);
    });

    return tab;
}
