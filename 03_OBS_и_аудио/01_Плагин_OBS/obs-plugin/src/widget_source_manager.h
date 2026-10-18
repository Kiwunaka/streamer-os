#pragma once

#include <QtGlobal>
#include <QString>

struct WidgetConfig {
    QString preset;
    QString theme;
    QString visualizer;
    QString accent;
    int opacity = 80;
    double sensitivity = 1.0;
    int glow = 50;
    double motion = 1.0;
    QString timer_mode;
    int timer_minutes = 10;
    qint64 timer_start = 0;
    QString timer_time;
    QString timer_title;
    QString timer_subtitle;
    QString timer_label;
};

struct WidgetTarget {
    QString source_name;
    QString file_name;
    int width = 0;
    int height = 0;
};

class WidgetSourceManager {
public:
    explicit WidgetSourceManager(QString data_dir);

    QString BuildUrl(const QString& file_name, const WidgetConfig& config) const;
    bool EnsureBrowserSource(const WidgetTarget& target, const WidgetConfig& config,
                             bool create_if_missing) const;

private:
    QString data_dir_;
};
