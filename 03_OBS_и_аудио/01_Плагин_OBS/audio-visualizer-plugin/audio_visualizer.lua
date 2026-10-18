--[[
    Audio Visualizer for OBS Studio
    Lua Script - v2.0 with Presets and Multilingual UI
    
    Place this file in: obs-studio/data/obs-plugins/audio-visualizer/
    Add via: Tools → Scripts → + → audio_visualizer.lua
]]--

obs = obslua

-- ==========================================
-- LOCALIZATION
-- ==========================================
local L = {}

L["en"] = {
    title = "🎵 Audio Visualizer",
    desc = "Real-time audio visualization for your stream!",
    status = "Status",
    online = "● ONLINE",
    offline = "○ OFFLINE",
    device = "Audio Device",
    bands = "FFT Bands",
    smoothing = "Smoothing",
    preset = "Widget Preset",
    opacity = "Background Opacity",
    sensitivity = "Visualizer Sensitivity",
    glow = "Glow Intensity",
    start = "▶ Start Server",
    stop = "⏹ Stop Server",
    restart = "🔄 Restart Server",
    add_overlay = "➕ Add Overlay (1920×1080)",
    add_widget = "➕ Add Widget (700×300)",
    websocket = "WebSocket",
    lang = "Language",
    presets = {
        vinyl = "Vinyl (Spinning Record)",
        pill = "Pill (Spotify Style)",
        glass = "Glass (Glassmorphism)",
        minimal = "Minimal (No Cover)"
    }
}

L["ru"] = {
    title = "🎵 Аудио Визуализатор",
    desc = "Визуализация звука в реальном времени!",
    status = "Статус",
    online = "● ОНЛАЙН",
    offline = "○ ОФФЛАЙН",
    device = "Аудио устройство",
    bands = "Полосы FFT",
    smoothing = "Сглаживание",
    preset = "Пресет виджета",
    opacity = "Прозрачность фона",
    sensitivity = "Чувствительность",
    glow = "Интенсивность свечения",
    start = "▶ Запустить сервер",
    stop = "⏹ Остановить сервер",
    restart = "🔄 Перезапустить",
    add_overlay = "➕ Добавить оверлей (1920×1080)",
    add_widget = "➕ Добавить виджет (700×300)",
    websocket = "WebSocket",
    lang = "Язык",
    presets = {
        vinyl = "Винил (Крутящаяся пластинка)",
        pill = "Пилюля (Стиль Spotify)",
        glass = "Стекло (Глассморфизм)",
        minimal = "Минимал (Без обложки)"
    }
}

-- Settings
local current_lang = "en"
local server_path = ""
local server_process = nil
local audio_device = ""
local num_bands = 128
local smoothing = 0.5
local port = 8765
local preset = "vinyl"
local opacity = 80
local sensitivity = 1.0
local glow = 50

-- Plugin directory
local script_dir = ""

-- Helper to get localized string
local function t(key)
    if L[current_lang] and L[current_lang][key] then
        return L[current_lang][key]
    end
    return L["en"][key] or key
end

-- Description
function script_description()
    return [[
<h2>🎵 Audio Visualizer / Аудио Визуализатор</h2>
<p>Real-time audio visualization for your stream!</p>
<p>Визуализация звука в реальном времени!</p>
<hr>
<p><b>v2.0</b> — Presets, Opacity, Sensitivity</p>
]]
end

-- Audio devices (placeholder)
function get_audio_devices()
    return {"Default", "VoiceMeeter Output", "Speakers", "Headphones"}
end

-- Properties UI
function script_properties()
    local props = obs.obs_properties_create()
    
    -- Language selector
    local lang_list = obs.obs_properties_add_list(props, "language", "🌐 Language / Язык",
        obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_STRING)
    obs.obs_property_list_add_string(lang_list, "English", "en")
    obs.obs_property_list_add_string(lang_list, "Русский", "ru")
    obs.obs_property_set_modified_callback(lang_list, function(props, prop, settings)
        current_lang = obs.obs_data_get_string(settings, "language")
        return true -- Refresh UI
    end)
    
    -- Separator
    obs.obs_properties_add_text(props, "sep0", "═══════════════════════════════", obs.OBS_TEXT_INFO)
    
    -- Audio device
    local device_list = obs.obs_properties_add_list(props, "audio_device", t("device"),
        obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_STRING)
    for _, device in ipairs(get_audio_devices()) do
        obs.obs_property_list_add_string(device_list, device, device)
    end
    
    -- FFT Bands
    local bands_list = obs.obs_properties_add_list(props, "num_bands", t("bands"),
        obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_INT)
    obs.obs_property_list_add_int(bands_list, "32", 32)
    obs.obs_property_list_add_int(bands_list, "64", 64)
    obs.obs_property_list_add_int(bands_list, "128 ★", 128)
    
    -- Smoothing
    obs.obs_properties_add_float_slider(props, "smoothing", t("smoothing"), 0.0, 0.9, 0.1)
    
    -- Separator
    obs.obs_properties_add_text(props, "sep1", "─────── " .. t("preset") .. " ───────", obs.OBS_TEXT_INFO)
    
    -- Preset selector
    local preset_list = obs.obs_properties_add_list(props, "preset", t("preset"),
        obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_STRING)
    obs.obs_property_list_add_string(preset_list, t("presets").vinyl, "vinyl")
    obs.obs_property_list_add_string(preset_list, t("presets").pill, "pill")
    obs.obs_property_list_add_string(preset_list, t("presets").glass, "glass")
    obs.obs_property_list_add_string(preset_list, t("presets").minimal, "minimal")
    
    -- Opacity
    obs.obs_properties_add_int_slider(props, "opacity", t("opacity") .. " %", 0, 100, 5)
    
    -- Sensitivity
    obs.obs_properties_add_float_slider(props, "sensitivity", t("sensitivity"), 0.5, 3.0, 0.1)
    
    -- Glow
    obs.obs_properties_add_int_slider(props, "glow", t("glow") .. " %", 0, 100, 5)
    
    -- Separator
    obs.obs_properties_add_text(props, "sep2", "═══════════════════════════════", obs.OBS_TEXT_INFO)
    
    -- Server controls
    obs.obs_properties_add_button(props, "start_server", t("start"), start_server_clicked)
    obs.obs_properties_add_button(props, "stop_server", t("stop"), stop_server_clicked)
    obs.obs_properties_add_button(props, "restart_server", t("restart"), restart_server_clicked)
    
    -- Separator
    obs.obs_properties_add_text(props, "sep3", "─────── Sources ───────", obs.OBS_TEXT_INFO)
    
    -- Quick Add buttons
    obs.obs_properties_add_button(props, "add_overlay", t("add_overlay"), add_overlay_clicked)
    obs.obs_properties_add_button(props, "add_widget", t("add_widget"), add_widget_clicked)
    
    -- Info
    obs.obs_properties_add_text(props, "info", t("websocket") .. ": ws://localhost:" .. port, obs.OBS_TEXT_INFO)
    
    return props
end

-- Defaults
function script_defaults(settings)
    obs.obs_data_set_default_string(settings, "language", "en")
    obs.obs_data_set_default_string(settings, "audio_device", "Default")
    obs.obs_data_set_default_int(settings, "num_bands", 128)
    obs.obs_data_set_default_double(settings, "smoothing", 0.5)
    obs.obs_data_set_default_string(settings, "preset", "vinyl")
    obs.obs_data_set_default_int(settings, "opacity", 80)
    obs.obs_data_set_default_double(settings, "sensitivity", 1.0)
    obs.obs_data_set_default_int(settings, "glow", 50)
end

-- Load settings
function script_update(settings)
    current_lang = obs.obs_data_get_string(settings, "language")
    audio_device = obs.obs_data_get_string(settings, "audio_device")
    num_bands = obs.obs_data_get_int(settings, "num_bands")
    smoothing = obs.obs_data_get_double(settings, "smoothing")
    preset = obs.obs_data_get_string(settings, "preset")
    opacity = obs.obs_data_get_int(settings, "opacity")
    sensitivity = obs.obs_data_get_double(settings, "sensitivity")
    glow = obs.obs_data_get_int(settings, "glow")
end

-- Script load
function script_load(settings)
    script_dir = script_path()
    obs.script_log(obs.LOG_INFO, "[AudioViz] Loaded from: " .. script_dir)
    server_path = script_dir .. "audio_server.exe"
    start_server()
end

-- Script unload
function script_unload()
    stop_server()
    obs.script_log(obs.LOG_INFO, "[AudioViz] Unloaded")
end

-- Start server
function start_server()
    if server_process then return end
    
    local cmd = ""
    local file = io.open(server_path, "r")
    if file then
        file:close()
        cmd = '"' .. server_path .. '" --bands ' .. num_bands
    else
        local py_path = script_dir .. "audio_visualizer_server.py"
        cmd = 'python "' .. py_path .. '"'
    end
    
    obs.script_log(obs.LOG_INFO, "[AudioViz] Starting: " .. cmd)
    os.execute('start /B "" ' .. cmd)
    server_process = true
end

-- Stop server
function stop_server()
    if not server_process then return end
    os.execute('FOR /F "tokens=5" %P IN (\'netstat -ano ^| findstr :' .. port .. '\') DO taskkill /F /PID %P 2>nul')
    server_process = nil
    obs.script_log(obs.LOG_INFO, "[AudioViz] Stopped")
end

-- Button callbacks
function start_server_clicked(props, p) start_server(); return true end
function stop_server_clicked(props, p) stop_server(); return true end
function restart_server_clicked(props, p) stop_server(); start_server(); return true end

-- Build URL with parameters
function build_widget_url(html_file)
    local base = "file:///" .. script_dir:gsub("\\", "/") .. html_file
    local params = string.format("?preset=%s&opacity=%d&sensitivity=%.1f&glow=%d", 
        preset, opacity, sensitivity, glow)
    return base .. params
end

-- Add overlay
function add_overlay_clicked(props, p)
    local scene_source = obs.obs_frontend_get_current_scene()
    if not scene_source then return true end
    local scene = obs.obs_scene_from_source(scene_source)
    
    local settings = obs.obs_data_create()
    obs.obs_data_set_string(settings, "url", build_widget_url("stream_overlay_premium.html"))
    obs.obs_data_set_int(settings, "width", 1920)
    obs.obs_data_set_int(settings, "height", 1080)
    obs.obs_data_set_bool(settings, "shutdown", false)
    
    local source = obs.obs_source_create("browser_source", "Audio Visualizer Overlay", settings, nil)
    obs.obs_scene_add(scene, source)
    
    obs.obs_data_release(settings)
    obs.obs_source_release(source)
    obs.obs_source_release(scene_source)
    return true
end

-- Add widget
function add_widget_clicked(props, p)
    local scene_source = obs.obs_frontend_get_current_scene()
    if not scene_source then return true end
    local scene = obs.obs_scene_from_source(scene_source)
    
    local settings = obs.obs_data_create()
    obs.obs_data_set_string(settings, "url", build_widget_url("widget_premium.html"))
    obs.obs_data_set_int(settings, "width", 700)
    obs.obs_data_set_int(settings, "height", 300)
    obs.obs_data_set_bool(settings, "shutdown", false)
    
    local source = obs.obs_source_create("browser_source", "Music Widget", settings, nil)
    obs.obs_scene_add(scene, source)
    
    obs.obs_data_release(settings)
    obs.obs_source_release(source)
    obs.obs_source_release(scene_source)
    return true
end
