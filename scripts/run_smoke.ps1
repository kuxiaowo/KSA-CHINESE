param([switch]$Fallback,[string]$Tag='final',[string]$Profile='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(!$Profile) { $Profile=Join-Path $taskRoot 'work\test-profile' }
$Profile=[IO.Path]::GetFullPath($Profile)
if(!$Profile.StartsWith((Join-Path $taskRoot 'work\'),[StringComparison]::OrdinalIgnoreCase) -or !$Profile.Contains('test-profile')) { throw 'Smoke is restricted to workspace test-profile directories.' }
if($Tag -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid log tag' }
& dotnet build (Join-Path $taskRoot 'src\SmokeDriver\SmokeDriver.csproj') -c Release --nologo "-p:BaseIntermediateOutputPath=$(Join-Path $taskRoot 'work\obj\SmokeDriver\')" "-p:OutputPath=$(Join-Path $taskRoot 'work\build\SmokeDriver\')"
if($LASTEXITCODE -ne 0) { throw 'Smoke driver build failed' }
$taskMods=Join-Path $Profile 'mods'
New-Item -ItemType Directory -Force -Path (Join-Path $taskMods 'SmokeDriver') | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'work\build\SmokeDriver\SmokeDriver.dll') -Destination (Join-Path $taskMods 'SmokeDriver') -Force
[IO.File]::WriteAllText((Join-Path $taskMods 'SmokeDriver\mod.toml'),"name = `"5602 Workspace Smoke Driver`"`nassets = []`n[StarMap]`nEntryAssembly = `"SmokeDriver`"`n",[Text.UTF8Encoding]::new($false))
$taskManifest="[[mods]]`nid = `"Core`"`nenabled = true`n"
if(!$Fallback) {
    Copy-Item -LiteralPath (Join-Path $taskRoot 'work\build\KsaChinese') -Destination $taskMods -Recurse -Force
    $taskManifest+="`n[[mods]]`nid = `"KsaChinese`"`nenabled = true`n"
}
$taskManifest+="`n[[mods]]`nid = `"SmokeDriver`"`nenabled = true`n"
[IO.File]::WriteAllText((Join-Path $Profile 'manifest.toml'),$taskManifest,[Text.UTF8Encoding]::new($false))
$taskOld=$env:KSA_SMOKE_FALLBACK
try {
    $env:KSA_SMOKE_FALLBACK=if($Fallback){'1'}else{'0'}
    $taskProcess=Start-Process -FilePath (Join-Path $taskRoot 'work\runtime\StarMap.exe') -WorkingDirectory (Join-Path $taskRoot 'work\runtime') -ArgumentList @('-InstancePath',('"'+$Profile+'"')) -RedirectStandardOutput (Join-Path $taskRoot "work\runtime\$Tag.stdout.log") -RedirectStandardError (Join-Path $taskRoot "work\runtime\$Tag.stderr.log") -WindowStyle Hidden -PassThru
    $taskProcess.Id | Set-Content -LiteralPath (Join-Path $taskRoot "work\runtime\$Tag.pid")
    Write-Output "Smoke process $($taskProcess.Id), profile $Profile"
} finally { $env:KSA_SMOKE_FALLBACK=$taskOld }
