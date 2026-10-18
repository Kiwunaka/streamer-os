param([Parameter(Mandatory=$true)][string]$Compiler, [Parameter(Mandatory=$true)][string]$NewtonsoftJson)
$ErrorActionPreference='Stop'
$NewtonsoftJson=(Resolve-Path -LiteralPath $NewtonsoftJson).Path
$build=Join-Path $PSScriptRoot 'build-providers'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$source=Join-Path $PSScriptRoot '..\02_Стримербот_и_CSharp\02_Экшены_и_ядро'
# Compile test copies with localhost endpoints. Production endpoints are unchanged.
foreach($name in @('FalImageClient','SpeechClient')) {
 $text=[System.IO.File]::ReadAllText((Join-Path $source "SharedCore\Providers\$name.cs"))
 $text=$text.Replace('https://queue.fal.run/','http://127.0.0.1:18409/')
 $text=$text.Replace('new Uri(url).Scheme != "https"','new Uri(url).Scheme != "http"')
 $text=$text.Replace('https://api.fish.audio/v1/tts','http://127.0.0.1:18409/fish').Replace('https://api.inworld.ai/tts/v1/voice','http://127.0.0.1:18409/inworld').Replace('https://api.elevenlabs.io/v1/text-to-speech/','http://127.0.0.1:18409/eleven/')
 [System.IO.File]::WriteAllText((Join-Path $build "$name.cs"),$text)
}
$framework="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& $Compiler /nologo /target:exe "/out:$build\ProviderTests.exe" "/r:$framework\System.Net.Http.dll" "/r:$framework\System.Web.Extensions.dll" "$build\FalImageClient.cs" "$build\SpeechClient.cs" "$PSScriptRoot\ProviderTests.cs"
if($LASTEXITCODE -ne 0){throw 'Provider tests did not compile.'}
$fixture=Start-Process -FilePath python -ArgumentList @('"'+(Join-Path $PSScriptRoot 'api_fixture.py')+'"') -PassThru -WindowStyle Hidden
try {
 $ready=$false
 for($i=0;$i -lt 30;$i++){try{Invoke-RestMethod 'http://127.0.0.1:18409/state' -TimeoutSec 1 | Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 100}}
 if(!$ready){throw 'Local fixture did not start.'}
 & "$build\ProviderTests.exe"
 if($LASTEXITCODE -ne 0){throw 'Provider tests failed.'}
} finally {if(!$fixture.HasExited){Stop-Process -Id $fixture.Id}}
$text=[System.IO.File]::ReadAllText((Join-Path $source 'StreamerOsSettings.cs')).Replace('public class CPHInline','public partial class CPHInline')
[System.IO.File]::WriteAllText((Join-Path $build 'Settings.cs'),$text)
& $Compiler /nologo /target:exe "/out:$build\SettingsTests.exe" "/r:$NewtonsoftJson" "$build\Settings.cs" "$PSScriptRoot\SettingsTests.cs"
if($LASTEXITCODE -ne 0){throw 'Settings tests need Roslyn C# 7 or newer. Pass -Compiler.'}
Copy-Item -LiteralPath $NewtonsoftJson -Destination (Join-Path $build 'Newtonsoft.Json.dll') -Force
& "$build\SettingsTests.exe"
if($LASTEXITCODE -ne 0){throw 'Settings tests failed.'}
$text=[System.IO.File]::ReadAllText((Join-Path $source 'AiModerator.cs')).Replace('public class CPHInline','public partial class CPHInline').Replace('http://127.0.0.1:1608','http://127.0.0.1:18409').Replace('"StreamerBot_AI_Cache"','"StreamerOS_Tests_NoMedia"')
[System.IO.File]::WriteAllText((Join-Path $build 'Moderator.cs'),$text)
& $Compiler /nologo /target:exe "/out:$build\ModeratorTests.exe" "/r:$NewtonsoftJson" "/r:$framework\System.Net.Http.dll" "$build\Moderator.cs" "$PSScriptRoot\ModeratorTests.cs"
if($LASTEXITCODE -ne 0){throw 'Moderator tests did not compile.'}
& "$build\ModeratorTests.exe"
if($LASTEXITCODE -ne 0){throw 'Moderator tests failed.'}
