param([switch]$ResetPaths)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$pathsFile = Join-Path $PSScriptRoot 'launcher.local.json'
function Select-Executable([string]$Title, [string]$DefaultPath) {
    if (![string]::IsNullOrWhiteSpace($DefaultPath) -and (Test-Path -LiteralPath $DefaultPath -PathType Leaf)) { return $DefaultPath }
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = $Title
    $dialog.Filter = 'Windows application (*.exe)|*.exe'
    try {
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Selection cancelled.' }
        return $dialog.FileName
    } finally { $dialog.Dispose() }
}
try {
    $paths = $null
    if (!$ResetPaths -and (Test-Path -LiteralPath $pathsFile)) {
        $paths = [System.IO.File]::ReadAllText($pathsFile) | ConvertFrom-Json
    }
    $obsPath = if ($paths) { [string]$paths.obs } else { Join-Path $env:ProgramFiles 'obs-studio\bin\64bit\obs64.exe' }
    $botPath = if ($paths) { [string]$paths.streamerbot } else { '' }
    $obsPath = Select-Executable 'Select obs64.exe' $obsPath
    $botPath = Select-Executable 'Select Streamer.bot.exe' $botPath
    if ([System.IO.Path]::GetFileName($obsPath) -ine 'obs64.exe') { throw 'Select obs64.exe.' }
    if ([System.IO.Path]::GetFileName($botPath) -ine 'Streamer.bot.exe') { throw 'Select Streamer.bot.exe.' }
    @{ obs = $obsPath; streamerbot = $botPath } | ConvertTo-Json | Set-Content -LiteralPath $pathsFile -Encoding UTF8
    foreach ($exe in @($botPath, $obsPath)) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($exe)
        if (!(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            Start-Process -FilePath $exe -WorkingDirectory ([System.IO.Path]::GetDirectoryName($exe))
        }
    }
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri 'http://127.0.0.1:1608/health' -TimeoutSec 1
            if ($health.runtime -eq 'enabled') { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'OBS runtime did not start. Install the native plugin, then enable Start runtime with OBS in the Audio Visualizer dock. See START-HERE.md.' }
    Start-Process 'http://127.0.0.1:1608/admin'
} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Streamer OS', 'OK', 'Warning') | Out-Null
    exit 1
}
