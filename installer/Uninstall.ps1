param()
$ErrorActionPreference='Stop'
$taskState=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'install-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskState.product -ne 'KSA-CHINESE') { throw '安装状态无效' }
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$taskMod=[IO.Path]::GetFullPath((Join-Path $taskRoot 'Profile\mods\KsaChinese'))
if(!$taskMod.StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '非法安装路径' }
if(Test-Path -LiteralPath $taskMod) {
    $taskBackup=Join-Path $taskRoot ('Backups\KsaChinese-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    $taskBackup=[IO.Path]::GetFullPath($taskBackup)
    if(!$taskBackup.StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '非法备份路径' }
    New-Item -ItemType Directory -Force -Path (Split-Path $taskBackup -Parent) | Out-Null
    Move-Item -LiteralPath $taskMod -Destination $taskBackup
}
[IO.File]::WriteAllText((Join-Path $taskRoot 'Profile\manifest.toml'),"[[mods]]`nid = `"Core`"`nenabled = true`n",[Text.UTF8Encoding]::new($false))
Write-Host '汉化已卸载到备份目录，独立配置和存档保留。Launch.cmd 可继续以英文打开这份独立档案；原版入口始终可用。需要完全移除时，关闭游戏后自行删除此安装目录。'
