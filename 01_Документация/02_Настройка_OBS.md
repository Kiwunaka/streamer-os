# Настройка OBS

Полный порядок установки и запуск панели — [начать здесь](../START-HERE.md).

## Где лежит OBS-часть проекта

- [03_OBS_и_аудио](../03_OBS_и_аудио)

Главные папки:

- [01_Плагин_OBS](../03_OBS_и_аудио/01_Плагин_OBS)
- [02_Tuna_и_музыка](../03_OBS_и_аудио/02_Tuna_и_музыка)

## Если DLL уже собрана

1. Скопируй `audio-visualizer-plugin.dll` из готового пакета (`OBS/obs-plugins/64bit`) или из своей сборки (`obs-plugin/build-local/bin/64bit`) в `obs-studio/obs-plugins/64bit/`
2. Скопируй [audio-visualizer-plugin](../03_OBS_и_аудио/01_Плагин_OBS/audio-visualizer-plugin) в `obs-studio/data/obs-plugins/audio-visualizer-plugin/`
3. Для ручной установки скопируй `Assets/intro_*` из папки персон в `obs-studio/data/obs-plugins/audio-visualizer-plugin/assets/`. В готовом пакете это уже сделано.
4. Перезапусти OBS

## Если хочешь собрать плагин сам

Открой папку [obs-plugin](../03_OBS_и_аудио/01_Плагин_OBS/obs-plugin) и выполни:

```powershell
.\build-local.cmd
```

Дерево исходников OBS и зависимости не включены в репозиторий. Перед сборкой подготовь их по [инструкции сборки](../03_OBS_и_аудио/01_Плагин_OBS/obs-plugin/README_BUILD.md); там же описаны параметры для другого расположения OBS/CMake. Готовые DLL и EXE в исходниках не хранятся.

## Что делать в OBS

1. Открой `Tools -> Streamer OS Runtime`
2. Во вкладке `Server` выбери устройство и нажми `Start`
3. Во вкладке `Runtime` нажми `Insert runtime`
4. Нажми `Test toast`
5. Нажми `Test persona switch`

## Музыка через Tuna

Главный файл:

- [tuna_universal_v7.user.js](../03_OBS_и_аудио/02_Tuna_и_музыка/tuna_universal_v7.user.js)

Что сделать:

1. Установи `Tampermonkey` или `Violentmonkey`
2. Установи userscript
3. Оставь настройки:
   - `Host = 127.0.0.1`
   - `Port = 1608`
   - `Endpoint = /`
4. Нажми `Tuna: Send test payload`
