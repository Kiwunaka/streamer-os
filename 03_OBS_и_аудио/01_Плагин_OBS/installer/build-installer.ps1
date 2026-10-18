param(
    [string]$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$scriptPath = Join-Path $PSScriptRoot "audio-visualizer-plugin.iss"

if (-not (Test-Path $IsccPath)) {
    Write-Error "ISCC.exe not found. Install Inno Setup 6 or pass -IsccPath."
    exit 1
}

if (-not (Test-Path $scriptPath)) {
    Write-Error "Installer script not found: $scriptPath"
    exit 1
}

& $IsccPath $scriptPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
