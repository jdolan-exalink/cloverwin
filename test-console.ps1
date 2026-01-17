#!/usr/bin/env pwsh
# Test CloverBridge en modo consola con output visible

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " CloverBridge - Test Console Mode" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Detener procesos previos
Write-Host "Deteniendo procesos previos..." -ForegroundColor Yellow
Get-Process -Name "CloverBridge" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Verificar compilación
if (-not (Test-Path ".\bin\Debug\net8.0-windows\CloverBridge.exe")) {
    Write-Host "ERROR: No se encontró CloverBridge.exe" -ForegroundColor Red
    Write-Host "Ejecuta: dotnet build" -ForegroundColor Yellow
    exit 1
}

Write-Host "Ejecutando CloverBridge..." -ForegroundColor Green
Write-Host ""
Write-Host "Presiona Ctrl+C para detener" -ForegroundColor DarkGray
Write-Host ""

# Ejecutar con output redirigido
& ".\bin\Debug\net8.0-windows\CloverBridge.exe" --console 2>&1 | ForEach-Object {
    Write-Host $_ -ForegroundColor White
}
