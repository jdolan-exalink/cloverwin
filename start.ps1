#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick start script for CloverBridge development
    
.DESCRIPTION
    Builds and runs CloverBridge in development mode
#>

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    CloverBridge - Quick Start          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = Join-Path $PSScriptRoot "CloverBridge.csproj"

# Verificar .NET SDK
Write-Host "🔍 Verificando .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = & dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK no encontrado" -ForegroundColor Red
    Write-Host "   Descargue .NET 8.0 SDK de: https://dot.net" -ForegroundColor Yellow
    exit 1
}

Write-Host "   ✅ .NET SDK $dotnetVersion instalado" -ForegroundColor Green
Write-Host ""

# Restaurar dependencias
Write-Host "📥 Restaurando dependencias..." -ForegroundColor Yellow
& dotnet restore $ProjectPath --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error restaurando dependencias" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ Dependencias restauradas" -ForegroundColor Green
Write-Host ""

# Build
Write-Host "🔨 Compilando proyecto..." -ForegroundColor Yellow
& dotnet build $ProjectPath --configuration Debug --no-restore --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error compilando proyecto" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ Proyecto compilado" -ForegroundColor Green
Write-Host ""

# Run
Write-Host "🚀 Iniciando CloverBridge en modo consola..." -ForegroundColor Yellow
Write-Host "   Presione Ctrl+C para detener" -ForegroundColor Gray
Write-Host ""

& dotnet run --project $ProjectPath --no-build -- --console
