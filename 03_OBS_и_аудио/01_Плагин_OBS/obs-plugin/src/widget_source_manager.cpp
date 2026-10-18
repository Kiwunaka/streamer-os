#include "widget_source_manager.h"

#include <obs-module.h>
#include <obs-frontend-api.h>
#include <QFileInfo>
#include <QUrl>
#include <QUrlQuery>

WidgetSourceManager::WidgetSourceManager(QString data_dir)
    : data_dir_(std::move(data_dir)) {}

QString WidgetSourceManager::BuildUrl(const QString& file_name, const WidgetConfig& config) const {
    const QString file_path = data_dir_ + "/" + file_name;
    QUrl url = QUrl::fromLocalFile(file_path);

    QUrlQuery query;
    auto add_if = [&query](const QString& key, const QString& value) {
        if (!value.isEmpty()) {
            query.addQueryItem(key, value);
        }
    };

    add_if("preset", config.preset);
    add_if("theme", config.theme);
    add_if("viz", config.visualizer);
    add_if("accent", config.accent);
    query.addQueryItem("opacity", QString::number(config.opacity));
    query.addQueryItem("sensitivity", QString::number(config.sensitivity, 'f', 1));
    query.addQueryItem("glow", QString::number(config.glow));
    query.addQueryItem("motion", QString::number(config.motion, 'f', 2));

    if (file_name.contains("stream_overlay")) {
        if (!config.timer_mode.isEmpty() && config.timer_mode != "auto") {
            add_if("timer", config.timer_mode);
        }
        if (config.timer_mode == "minutes") {
            query.addQueryItem("minutes", QString::number(config.timer_minutes));
        } else if (config.timer_mode == "start") {
            query.addQueryItem("start", QString::number(config.timer_start));
        } else if (config.timer_mode == "time") {
            add_if("time", config.timer_time);
        }
        add_if("title", config.timer_title);
        add_if("subtitle", config.timer_subtitle);
        add_if("label", config.timer_label);
    }
    url.setQuery(query);

    return url.toString();
}

bool WidgetSourceManager::EnsureBrowserSource(const WidgetTarget& target, const WidgetConfig& config,
                                              bool create_if_missing) const {
    if (target.source_name.isEmpty()) {
        return false;
    }

    const QString url = BuildUrl(target.file_name, config);
    obs_data_t* settings = obs_data_create();
    obs_data_set_string(settings, "url", url.toUtf8().constData());
    obs_data_set_int(settings, "width", target.width);
    obs_data_set_int(settings, "height", target.height);
    obs_data_set_bool(settings, "shutdown", false);

    obs_source_t* existing = obs_get_source_by_name(target.source_name.toUtf8().constData());
    if (existing) {
        obs_source_update(existing, settings);
        obs_source_release(existing);
        obs_data_release(settings);
        return true;
    }

    if (!create_if_missing) {
        obs_data_release(settings);
        return false;
    }

    obs_source_t* scene_source = obs_frontend_get_current_scene();
    if (!scene_source) {
        obs_data_release(settings);
        return false;
    }

    obs_scene_t* scene = obs_scene_from_source(scene_source);
    if (!scene) {
        obs_source_release(scene_source);
        obs_data_release(settings);
        return false;
    }

    obs_source_t* source = obs_source_create("browser_source",
                                            target.source_name.toUtf8().constData(),
                                            settings,
                                            nullptr);
    if (source) {
        obs_scene_add(scene, source);
        obs_source_release(source);
    }

    obs_source_release(scene_source);
    obs_data_release(settings);
    return source != nullptr;
}
