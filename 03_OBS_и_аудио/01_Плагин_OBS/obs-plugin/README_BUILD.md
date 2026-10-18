# Сборка нативного OBS-плагина

Нужны Windows, CMake 3.20+, Visual Studio 2022 Build Tools с C++ и Windows SDK, Qt 6 Core/Widgets/Gui/Network и подготовленное дерево OBS с заголовками и x64-библиотеками `obs.lib` / `obs-frontend-api.lib`. В зависимостях нужны также websocketpp и standalone Asio. Исходники OBS, инструменты и зависимости не хранятся в этом репозитории.

## С существующим деревом OBS

Из этой папки:

```powershell
.\build-local.ps1 -CMakePath 'C:\Tools\CMake\bin\cmake.exe' -ObsSourceRoot 'D:\src\obs-studio' -ObsBuildRoot 'D:\src\obs-studio\build_x64'
```

Если CMake находится в PATH, `-CMakePath` можно не указывать. По умолчанию `ObsSourceRoot` — соседняя папка `../obs-studio`, а `ObsBuildRoot` — `build_x64` внутри неё.

Скрипт использует существующую схему проекта: Visual Studio 2022, x64, `RelWithDebInfo`. В OBS build должны быть `config/obsconfig.h` и библиотеки этой конфигурации. Сохранённый CMakeLists.txt ищет OBS dependencies и Qt в `.deps/obs-deps-2025-08-23-x64` и `.deps/obs-deps-qt6-2025-08-23-x64`.

## С другим расположением зависимостей

Пути можно явно передать через CMake:

```powershell
cmake -S . -B build-local -G 'Visual Studio 17 2022' -A x64 -DSTREAMER_OS_LOCAL_OBS_ROOT='D:/src/obs-studio' -DSTREAMER_OS_LOCAL_OBS_BUILD='D:/src/obs-studio/build_x64' -DOBS_DEPS_ROOT='D:/obs-deps' -DCMAKE_PREFIX_PATH='D:/Qt/6/lib/cmake'
cmake --build build-local --config RelWithDebInfo
```

Готовая DLL: `build-local/bin/64bit/audio-visualizer-plugin.dll`.

Скопируй DLL в `<OBS>/obs-plugins/64bit/`, а содержимое соседней папки `audio-visualizer-plugin` — в `<OBS>/data/obs-plugins/audio-visualizer-plugin/`. Для нативного варианта нужны HTML, JS, CSS и папка icons. В data-каталог также скопируй `Assets/intro_*` из папки персон в подкаталог `assets`. Из корня репозитория `./package.ps1` собирает готовую структуру OBS и скрипты в ZIP. `audio_server.exe` и Lua-скрипт ему не нужны.

Дальше — [настройка OBS](../../../01_Документация/02_Настройка_OBS.md).
