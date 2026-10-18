# Сборка установщика на Windows

Нужен установленный Inno Setup 6. Сначала собери [нативный OBS-плагин](../obs-plugin/README_BUILD.md).

Из этой папки выполни:

```powershell
.\build-native-installer.ps1
```

Если Inno Setup расположен в другом месте:

```powershell
.\build-native-installer.ps1 -IsccPath 'D:\Tools\Inno Setup 6\ISCC.exe'
```

Установщик создаётся в `dist/Audio-Visualizer-OBS-Native-Setup.exe`. Он берёт DLL из `../obs-plugin/build-local/bin/64bit` и HTML/userscript/icons из `../audio-visualizer-plugin`.

При установке выбери корневую папку OBS, в которой находится `bin/64bit/obs64.exe`. Файлы будут установлены в `obs-plugins/64bit/` и `data/obs-plugins/audio-visualizer-plugin/`.

`build-installer.ps1` и `audio-visualizer-plugin.iss` сохранены для прежнего Python/Lua варианта AudioViz. Ему требуется отдельно собранный `audio_server.exe`, который не включён в репозиторий исходников. Для Streamer OS используй нативный вариант выше.
