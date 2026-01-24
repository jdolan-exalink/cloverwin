param(
    [string]$Configuration = "Release",
    [string]$Output = ".\dist\v1.3.1"
)

$ErrorActionPreference = "Stop"

Write-Host "----------------------------------------" -ForegroundColor Cyan
Write-Host "    CloverBridge - Build Script         " -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = Join-Path $PSScriptRoot "CloverBridge.csproj"

Write-Host "Building project..." -ForegroundColor Yellow
Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
Write-Host "   Output: $Output" -ForegroundColor Gray
Write-Host ""

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore $ProjectPath

Write-Host "Building and publishing..." -ForegroundColor Yellow
& dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $Output `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishReadyToRun=true

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Path: $Output" -ForegroundColor Gray
