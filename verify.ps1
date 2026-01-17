#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifica que el proyecto esté listo para compilar
    
.DESCRIPTION
    Chequea requisitos y estructura del proyecto
#>

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    CloverBridge - Project Verification ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$allOk = $true

# 1. Verificar .NET SDK
Write-Host "🔍 Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ .NET SDK $dotnetVersion found" -ForegroundColor Green
    } else {
        Write-Host "   ❌ .NET SDK not found" -ForegroundColor Red
        Write-Host "   Download from: https://dot.net" -ForegroundColor Yellow
        $allOk = $false
    }
} catch {
    Write-Host "   ❌ .NET SDK not found" -ForegroundColor Red
    $allOk = $false
}

Write-Host ""

# 2. Verificar archivos del proyecto
Write-Host "🔍 Checking project files..." -ForegroundColor Yellow

$requiredFiles = @(
    "CloverBridge.csproj",
    "Program.cs",
    "appsettings.json",
    "Models\AppConfig.cs",
    "Models\CloverMessages.cs",
    "Services\ConfigurationService.cs",
    "Services\CloverWebSocketService.cs",
    "Services\ApiService.cs",
    "Services\TransactionQueueService.cs",
    "Services\InboxWatcherService.cs",
    "UI\TrayApplicationContext.cs",
    "UI\PairingWindow.xaml",
    "UI\PairingWindow.xaml.cs"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $PSScriptRoot $file
    if (Test-Path $path) {
        Write-Host "   OK $file" -ForegroundColor Green
    } else {
        Write-Host "   MISSING $file" -ForegroundColor Red
        $allOk = $false
    }
}

Write-Host ""

# 3. Verificar scripts
Write-Host "🔍 Checking scripts..." -ForegroundColor Yellow

$scripts = @(
    "build.ps1",
    "install-service.ps1",
    "start.ps1"
)

foreach ($script in $scripts) {
    $path = Join-Path $PSScriptRoot $script
    if (Test-Path $path) {
        Write-Host "   ✅ $script" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  $script (missing)" -ForegroundColor Yellow
    }
}

Write-Host ""

# 4. Verificar documentación
Write-Host "🔍 Checking documentation..." -ForegroundColor Yellow

$docs = @(
    "README.md",
    "QUICK_START.md",
    "INSTALL_SERVICE.md",
    "MIGRACION_RESUMEN.md"
)

foreach ($doc in $docs) {
    $path = Join-Path $PSScriptRoot $doc
    if (Test-Path $path) {
        Write-Host "   ✅ $doc" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  $doc (missing)" -ForegroundColor Yellow
    }
}

Write-Host ""

# 5. Contar líneas de código
Write-Host "📊 Code statistics..." -ForegroundColor Yellow

$csharpFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Include "*.cs" -File
$xamlFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Include "*.xaml" -File
$totalLines = 0

foreach ($file in ($csharpFiles + $xamlFiles)) {
    $lines = (Get-Content $file).Count
    $totalLines += $lines
}

Write-Host "   C# files:    $($csharpFiles.Count)" -ForegroundColor Gray
Write-Host "   XAML files:  $($xamlFiles.Count)" -ForegroundColor Gray
Write-Host "   Total lines: $totalLines" -ForegroundColor Gray

Write-Host ""

# 6. Resumen
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
if ($allOk) {
    Write-Host "✅ Project is ready to build!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. .\start.ps1         - Quick start (dev mode)" -ForegroundColor Cyan
    Write-Host "  2. .\build.ps1         - Build release version" -ForegroundColor Cyan
    Write-Host "  3. .\install-service.ps1 - Install as Windows Service" -ForegroundColor Cyan
} else {
    Write-Host "❌ Some issues found. Please fix them before building." -ForegroundColor Red
}
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
