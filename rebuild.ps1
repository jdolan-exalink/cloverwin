$ErrorActionPreference = "Stop"
Write-Host "--- Re-compilando DIST con fix de persistencia ---" -ForegroundColor Cyan
$DistPath = Join-Path $PSScriptRoot "dist"
$WinX64Path = Join-Path $DistPath "win-x64"
$WinX86Path = Join-Path $DistPath "win-x86"

if (Test-Path $WinX64Path) { Remove-Item -Recurse -Force $WinX64Path }
if (Test-Path $WinX86Path) { Remove-Item -Recurse -Force $WinX86Path }

New-Item -ItemType Directory -Path $WinX64Path -Force | Out-Null
New-Item -ItemType Directory -Path $WinX86Path -Force | Out-Null

$ProjectPath = Join-Path $PSScriptRoot "CloverBridge.csproj"

Write-Host ">>> Compilando win-x64..."
dotnet publish $ProjectPath --configuration Release --runtime win-x64 --self-contained true --output $WinX64Path /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:PublishReadyToRun=true

Write-Host ">>> Compilando win-x86..."
dotnet publish $ProjectPath --configuration Release --runtime win-x86 --self-contained true --output $WinX86Path /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:PublishReadyToRun=true

Write-Host "DONE: Fix aplicado en DIST"
