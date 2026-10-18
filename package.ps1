# Собрать переносимый пакет после сборки native-плагина. Не устанавливает файлы в OBS.
param([string]$Destination = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$obs = Join-Path $PSScriptRoot '03_OBS_и_аудио\01_Плагин_OBS'
$dll = Join-Path $obs 'obs-plugin\build-local\bin\64bit\audio-visualizer-plugin.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Сначала собери native-плагин: obs-plugin/build-local.ps1.' }
& (Join-Path $PSScriptRoot '02_Стримербот_и_CSharp\02_Экшены_и_ядро\SharedCore\build-shared-core.cmd')
if ($LASTEXITCODE -ne 0) { throw 'Не собрана StreamerOs.Shared.dll.' }
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
# A fresh staging folder avoids stale files from previous builds.
$stageRoot = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
$stage = Join-Path $stageRoot ('package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$pluginBin = Join-Path $stage 'OBS\obs-plugins\64bit'
$pluginData = Join-Path $stage 'OBS\data\obs-plugins\audio-visualizer-plugin'
New-Item -ItemType Directory -Force -Path $pluginBin,$pluginData | Out-Null
Copy-Item -LiteralPath $dll -Destination $pluginBin
foreach ($pattern in @('*.html','*.css','*.js')) {
    Get-ChildItem -LiteralPath (Join-Path $obs 'audio-visualizer-plugin') -Filter $pattern -File | Copy-Item -Destination $pluginData
}
Copy-Item -LiteralPath (Join-Path $obs 'audio-visualizer-plugin\icons') -Destination $pluginData -Recurse
$assets = Join-Path $pluginData 'assets'
New-Item -ItemType Directory -Path $assets | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '02_Стримербот_и_CSharp\01_Персоны_и_донаты\Assets') -Filter 'intro_*' -File | Copy-Item -Destination $assets
$bot = Join-Path $stage 'Streamer.bot'
New-Item -ItemType Directory -Path $bot | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '02_Стримербот_и_CSharp\02_Экшены_и_ядро') -Filter '*.cs' -File | Copy-Item -Destination $bot
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '02_Стримербот_и_CSharp\01_Персоны_и_донаты') -Filter '*.cs' -File | Copy-Item -Destination $bot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '02_Стримербот_и_CSharp\02_Экшены_и_ядро\SharedCore\bin\StreamerOs.Shared.dll') -Destination $bot
foreach ($file in @('START-HERE.md','README.md','start-streamer-os.cmd','start-streamer-os.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '01_Документация') -Destination $stage -Recurse
# Include tracked sources needed by the documentation, never local keys or build trees.
$sourceFiles = & git -C $PSScriptRoot -c core.quotepath=false ls-files -- '02_Стримербот_и_CSharp' '03_OBS_и_аудио' 'tests'
if ($LASTEXITCODE -ne 0) { throw 'Run packaging from a Git checkout.' }
foreach ($relative in $sourceFiles) {
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relative) -Destination $target
}

$archive = Join-Path $Destination 'Streamer-OS-Windows.zip'
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stage).FullName -DestinationPath $archive -Force
Write-Host $archive
