param()
$ErrorActionPreference='Stop'
function Get-TaskSha256([string]$Path) {
    $taskStream=[IO.File]::OpenRead($Path)
    $taskHasher=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskStream)).Replace('-','').ToLowerInvariant() }
    finally { $taskStream.Dispose(); $taskHasher.Dispose() }
}
$taskRoot=$PSScriptRoot
$taskState=Get-Content -LiteralPath (Join-Path $taskRoot 'install-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskState.product -ne 'KSA-CHINESE') { throw '安装状态无效' }
$taskMod=Join-Path $taskRoot 'Profile\mods\KsaChinese'
if(Test-Path -LiteralPath $taskMod) {
    $taskCompatibility=Get-Content -LiteralPath (Join-Path $taskMod 'compatibility.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach($taskProperty in $taskCompatibility.files.PSObject.Properties) {
        $taskFile=Join-Path $taskState.game $taskProperty.Name
        if(!(Test-Path -LiteralPath $taskFile) -or (Get-TaskSha256 $taskFile) -ne $taskProperty.Value) { throw '游戏已变化，汉化安全停止。可以使用原版入口启动；等待新版适配。' }
    }
}
Push-Location (Join-Path $taskRoot 'Loader')
try { & dotnet.exe '.\StarMap.dll' '-InstancePath' (Join-Path $taskRoot 'Profile'); exit $LASTEXITCODE }
finally { Pop-Location }
