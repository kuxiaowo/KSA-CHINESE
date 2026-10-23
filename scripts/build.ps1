param([string]$GameDir='F:\系统应用\Kitten Space Agency',[string]$LoaderDir='', [switch]$Validate)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(!$LoaderDir) { $LoaderDir=Join-Path $taskRoot '.tools\StarMap-0.4.8' }
$taskExpected=(Get-Content -LiteralPath (Join-Path $taskRoot 'assets\KsaChinese\compatibility.json') -Raw | ConvertFrom-Json).files.'KSA.dll'
if((Get-FileHash -LiteralPath (Join-Path $GameDir 'KSA.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected) { throw 'Uninspected KSA.dll; build/validation stopped.' }
$taskLauncher=Join-Path $taskRoot 'work\build\Launcher'
& dotnet build (Join-Path $taskRoot 'src\Launcher\Launcher.csproj') -c Release --nologo "-p:BaseIntermediateOutputPath=$(Join-Path $taskRoot 'work\obj\Launcher\')" "-p:OutputPath=$taskLauncher\"
if($LASTEXITCODE -ne 0) { throw 'Launcher build failed' }
$taskOutput=Join-Path $taskRoot 'work\build\KsaChinese'
& dotnet build (Join-Path $taskRoot 'src\KsaChinese\KsaChinese.csproj') -c Release --nologo "-p:KsaDir=$GameDir" "-p:LoaderDir=$LoaderDir" "-p:BaseIntermediateOutputPath=$(Join-Path $taskRoot 'work\obj\KsaChinese\')" "-p:OutputPath=$taskOutput\"
if($LASTEXITCODE -ne 0) { throw 'Plugin build failed' }
Copy-Item -Path (Join-Path $taskRoot 'assets\KsaChinese\*') -Destination $taskOutput -Recurse -Force
if($Validate) {
    $taskValidation=Join-Path $taskRoot 'work\build\Validation'
    & dotnet build (Join-Path $taskRoot 'src\Validation\Validation.csproj') -c Release --nologo "-p:KsaDir=$GameDir" "-p:LoaderDir=$LoaderDir" "-p:BaseIntermediateOutputPath=$(Join-Path $taskRoot 'work\obj\Validation\')" "-p:OutputPath=$taskValidation\"
    if($LASTEXITCODE -ne 0) { throw 'Validation build failed' }
    $taskPreviousRoot=$env:KSA_PROJECT_ROOT; $taskPreviousGame=$env:KSA_GAME_DIR; $taskPreviousLoader=$env:KSA_LOADER_DIR
    try {
        $env:KSA_PROJECT_ROOT=$taskRoot; $env:KSA_GAME_DIR=$GameDir; $env:KSA_LOADER_DIR=$LoaderDir
        & dotnet (Join-Path $taskValidation 'Validation.dll')
        if($LASTEXITCODE -ne 0) { throw 'Validation failed' }
    } finally { $env:KSA_PROJECT_ROOT=$taskPreviousRoot; $env:KSA_GAME_DIR=$taskPreviousGame; $env:KSA_LOADER_DIR=$taskPreviousLoader }
}
