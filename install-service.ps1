#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install CloverBridge as Windows Service
    
.DESCRIPTION
    Installs and starts CloverBridge as a Windows Service
    
.PARAMETER Uninstall
    Uninstalls the service
#>

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# Verificar permisos de administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "❌ Este script requiere permisos de administrador" -ForegroundColor Red
    Write-Host "   Ejecute PowerShell como administrador e intente nuevamente" -ForegroundColor Yellow
    exit 1
}

$ServiceName = "CloverBridge"
$ExePath = Join-Path $PSScriptRoot "bin\publish\CloverBridge.exe"

if ($Uninstall) {
    Write-Host "🗑️  Desinstalando servicio CloverBridge..." -ForegroundColor Yellow
    
    # Detener servicio si está corriendo
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Write-Host "   Deteniendo servicio..." -ForegroundColor Gray
            Stop-Service -Name $ServiceName -Force
        }
        
        Write-Host "   Eliminando servicio..." -ForegroundColor Gray
        sc.exe delete $ServiceName
        
        Write-Host "✅ Servicio desinstalado" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Servicio no encontrado" -ForegroundColor Yellow
    }
    
    exit 0
}

# Instalar servicio
Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    CloverBridge - Service Installer    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ExePath)) {
    Write-Host "❌ Ejecutable no encontrado: $ExePath" -ForegroundColor Red
    Write-Host "   Ejecute .\build.ps1 primero" -ForegroundColor Yellow
    exit 1
}

# Verificar si el servicio ya existe
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "⚠️  El servicio ya existe" -ForegroundColor Yellow
    $response = Read-Host "¿Desea reinstalarlo? (s/n)"
    if ($response -ne "s") {
        exit 0
    }
    
    Write-Host "   Deteniendo servicio existente..." -ForegroundColor Gray
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    
    Write-Host "   Eliminando servicio existente..." -ForegroundColor Gray
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

Write-Host "📦 Instalando servicio..." -ForegroundColor Yellow
Write-Host "   Nombre: $ServiceName" -ForegroundColor Gray
Write-Host "   Ejecutable: $ExePath" -ForegroundColor Gray
Write-Host ""

# Crear servicio
$binPath = "$ExePath --service"
sc.exe create $ServiceName binPath="$binPath" start=auto
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error creando servicio" -ForegroundColor Red
    exit 1
}

# Configurar descripción
sc.exe description $ServiceName "Bridge entre ERP y terminal Clover POS"

Write-Host "✅ Servicio instalado" -ForegroundColor Green
Write-Host ""

# Iniciar servicio
Write-Host "🚀 Iniciando servicio..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

Start-Sleep -Seconds 2

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "✅ Servicio iniciado correctamente" -ForegroundColor Green
} else {
    Write-Host "⚠️  El servicio no se está ejecutando" -ForegroundColor Yellow
    Write-Host "   Estado: $($service.Status)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "📋 Información del servicio:" -ForegroundColor Cyan
Write-Host "   Nombre: $ServiceName" -ForegroundColor Gray
Write-Host "   Estado: $($service.Status)" -ForegroundColor Gray
Write-Host "   Inicio: Automático" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Comandos útiles:" -ForegroundColor Yellow
Write-Host "   Ver logs:         Get-EventLog -LogName Application -Source CloverBridge" -ForegroundColor Cyan
Write-Host "   Detener:          Stop-Service CloverBridge" -ForegroundColor Cyan
Write-Host "   Iniciar:          Start-Service CloverBridge" -ForegroundColor Cyan
Write-Host "   Estado:           Get-Service CloverBridge" -ForegroundColor Cyan
Write-Host "   Desinstalar:      .\install-service.ps1 -Uninstall" -ForegroundColor Cyan
Write-Host ""
Write-Host "✨ Instalación completada!" -ForegroundColor Green
