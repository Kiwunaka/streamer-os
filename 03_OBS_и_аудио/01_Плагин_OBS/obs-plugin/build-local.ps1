param(
    [string]$CMakePath = 'cmake',
    [string]$ObsSourceRoot = (Join-Path $PSScriptRoot '../obs-studio'),
    [string]$ObsBuildRoot = ''
)
$ErrorActionPreference = 'Stop'
$ObsSourceRoot = (Resolve-Path -LiteralPath $ObsSourceRoot).Path
if ([string]::IsNullOrWhiteSpace($ObsBuildRoot)) {
    $ObsBuildRoot = Join-Path $ObsSourceRoot 'build_x64'
}
$ObsBuildRoot = (Resolve-Path -LiteralPath $ObsBuildRoot).Path
$buildDir = Join-Path $PSScriptRoot 'build-local'

& $CMakePath -S $PSScriptRoot -B $buildDir -G 'Visual Studio 17 2022' -A x64 `
    "-DSTREAMER_OS_LOCAL_OBS_ROOT=$ObsSourceRoot" `
    "-DSTREAMER_OS_LOCAL_OBS_BUILD=$ObsBuildRoot"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $CMakePath --build $buildDir --config RelWithDebInfo
exit $LASTEXITCODE
