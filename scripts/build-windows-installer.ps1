param(
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\SmartBrightness.App\SmartBrightness.App.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$installerScript = Join-Path $repoRoot 'installer\SmartBrightnessAI.iss'

$localSdkHost = Join-Path $repoRoot '.dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdkHost) {
    $dotnet = $localSdkHost
    $systemHost = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($systemHost) { $env:DOTNET_ROOT = Split-Path -Parent $systemHost.Source }
} else {
    $systemHost = Get-Command dotnet.exe -ErrorAction Stop
    $dotnet = $systemHost.Source
}

if (-not $InnoCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $InnoCompiler = $command.Source }
}
if (-not $InnoCompiler) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    )
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoCompiler) { throw 'Inno Setup 7 compiler (ISCC.exe) not found. Install Inno Setup 7 or pass -InnoCompiler.' }

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
& $dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

& $InnoCompiler $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compiler failed with exit code $LASTEXITCODE" }

$output = Join-Path $repoRoot 'artifacts\windows\installer\SmartBrightnessAI-Setup-0.2.0.exe'
if (-not (Test-Path -LiteralPath $output)) { throw "Installer output was not created: $output" }
Get-Item -LiteralPath $output | Select-Object FullName, Length, LastWriteTime
