#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build CloverBridge executable
    
.DESCRIPTION
    Compiles the C# project into a single-file executable
    
.PARAMETER Configuration
    Build configuration (Debug or Release)
    
.PARAMETER Output
    Output directory
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [string]$Output = ".\bin\publish"
)

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    CloverBridge - Build Script         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = Join-Path $PSScriptRoot "CloverBridge.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Host "❌ Project file not found: $ProjectPath" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Building project..." -ForegroundColor Yellow
Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
Write-Host "   Output: $Output" -ForegroundColor Gray
Write-Host ""

# Restore packages
Write-Host "📥 Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore $ProjectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Restore failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Packages restored" -ForegroundColor Green
Write-Host ""

# Build and publish
Write-Host "🔨 Building and publishing..." -ForegroundColor Yellow
& dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $Output `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host ""

$ExePath = Join-Path $Output "CloverBridge.exe"
if (Test-Path $ExePath) {
    $FileSize = (Get-Item $ExePath).Length / 1MB
    Write-Host "📋 Executable details:" -ForegroundColor Cyan
    Write-Host "   Path: $ExePath" -ForegroundColor Gray
    Write-Host "   Size: $([math]::Round($FileSize, 2)) MB" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🚀 To run:" -ForegroundColor Yellow
    Write-Host "   Normal mode:   .\CloverBridge.exe" -ForegroundColor Cyan
    Write-Host "   Console mode:  .\CloverBridge.exe --console" -ForegroundColor Cyan
    Write-Host "   Service mode:  .\CloverBridge.exe --service" -ForegroundColor Cyan
} else {
    Write-Host "⚠️  Executable not found at expected location" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✨ Done!" -ForegroundColor Green
