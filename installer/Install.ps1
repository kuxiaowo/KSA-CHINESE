param([string]$GameDir='', [string]$Destination='')
$ErrorActionPreference='Stop'
function Get-TaskSha256([string]$Path) {
    $taskStream=[IO.File]::OpenRead($Path)
    $taskHasher=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskStream)).Replace('-','').ToLowerInvariant() }
    finally { $taskStream.Dispose(); $taskHasher.Dispose() }
}
$taskPackage=Split-Path $PSScriptRoot -Parent
$taskPayloadToml=Get-Content -LiteralPath (Join-Path $taskPackage 'PlayerPayload\KsaChinese\mod.toml') -Raw -Encoding UTF8
if($taskPayloadToml -notmatch '(?m)^version\s*=\s*"([0-9.]+)"\s*$') { throw 'Mod version missing' }
$taskPayloadVersion=$Matches[1]
if(!$GameDir) {
    $taskDefault=Join-Path $env:ProgramFiles 'Kitten Space Agency'
    if(Test-Path -LiteralPath (Join-Path $taskDefault 'KSA.dll')) { $GameDir=$taskDefault }
    else { $GameDir=Read-Host '请输入含 KSA.dll 的游戏目录' }
}
$GameDir=(Resolve-Path -LiteralPath $GameDir.Trim('"')).Path
$taskCompatibility=Get-Content -LiteralPath (Join-Path $taskPackage 'PlayerPayload\KsaChinese\compatibility.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($taskProperty in $taskCompatibility.files.PSObject.Properties) {
    $taskFile=Join-Path $GameDir $taskProperty.Name
    if(!(Test-Path -LiteralPath $taskFile) -or (Get-TaskSha256 $taskFile) -ne $taskProperty.Value) { throw "游戏文件不符合已检查的 5602 版本：$($taskProperty.Name)" }
}
if(!$Destination) { $Destination=Join-Path $env:LOCALAPPDATA 'KsaChinese\Build5602' }
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination) { throw '安装目标已存在。请选一个新目录；安装器不会覆盖现有配置或存档。' }
if($Destination.TrimEnd('\') -eq $GameDir.TrimEnd('\') -or $Destination.StartsWith($GameDir.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '请选择游戏安装目录之外的独立汉化目录。' }
$taskDotnet=(Get-Command dotnet.exe -ErrorAction Stop).Source
if(!((& $taskDotnet --list-runtimes) -match '^Microsoft.NETCore.App 10\.')) { throw '需要 Microsoft .NET 10 x64 Runtime。' }
New-Item -ItemType Directory -Path $Destination | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Destination 'Loader'),(Join-Path $Destination 'Profile\mods') | Out-Null
Copy-Item -Path (Join-Path $taskPackage 'Loader\*') -Destination (Join-Path $Destination 'Loader') -Recurse
Copy-Item -LiteralPath (Join-Path $taskPackage 'PlayerPayload\KsaChinese') -Destination (Join-Path $Destination 'Profile\mods') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Launch.ps1'),(Join-Path $PSScriptRoot 'Launch.cmd'),(Join-Path $PSScriptRoot 'Uninstall.ps1'),(Join-Path $PSScriptRoot 'Uninstall.cmd') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $taskPackage 'README-安装卸载.md') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $taskPackage 'LICENSES') -Destination $Destination -Recurse
[IO.File]::WriteAllText((Join-Path $Destination 'Loader\StarMapConfig.json'),(@{GameLocation=(Join-Path $GameDir 'KSA.dll');RepositoryLocation='';GameArguments=@()}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Destination 'Profile\manifest.toml'),"[[mods]]`nid = `"Core`"`nenabled = true`n`n[[mods]]`nid = `"KsaChinese`"`nenabled = true`n",[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Destination 'install-state.json'),(@{product='KSA-CHINESE';version=$taskPayloadVersion;game=$GameDir;profile=(Join-Path $Destination 'Profile');installed_at=[DateTimeOffset]::UtcNow.ToString('O')}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Host "已安装到 $Destination。双击 Launch.cmd 启动。使用独立玩家目录，个人原存档没有复制或修改。"
