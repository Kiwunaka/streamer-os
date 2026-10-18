@echo off
setlocal

set OUT=%~dp0bin
if not exist "%OUT%" mkdir "%OUT%"

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set SYSWEB=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll

if not exist "%CSC%" (
  echo csc.exe not found at %CSC%
  exit /b 1
)

"%CSC%" /nologo /target:library /out:"%OUT%\StreamerOs.Shared.dll" /r:"%SYSWEB%" ^
  "%~dp0StateModels.cs" ^
  "%~dp0StateSerializer.cs" ^
  "%~dp0StateCompatibility.cs" ^
  "%~dp0LegacyStateMapper.cs" ^
  "%~dp0EventModels.cs" ^
  "%~dp0ProviderContracts.cs" ^
  "%~dp0Contracts\RuntimeEventEnvelope.cs" ^
  "%~dp0Runtime\VisualEventQueue.cs"

exit /b %errorlevel%
