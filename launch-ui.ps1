# Launch CloverBridge with Testing UI
# Run as: .\launch-ui.ps1

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   CloverBridge - Testing UI Launcher" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Detener procesos existentes
Write-Host "[1/3] Deteniendo procesos anteriores..." -ForegroundColor Yellow
Get-Process -Name "CloverBridge" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Compilar si es necesario
$exePath = ".\bin\Debug\net8.0-windows\CloverBridge.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "[2/3] Compilando aplicación..." -ForegroundColor Yellow
    dotnet build CloverBridge.csproj --configuration Debug --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Error en compilación" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "[2/3] ✓ Ejecutable encontrado" -ForegroundColor Green
}

# Lanzar aplicación con UI
Write-Host "[3/3] Iniciando Testing UI..." -ForegroundColor Yellow
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   UI de Testing iniciada" -ForegroundColor Green
Write-Host "   Modo: Testing Dashboard (--ui)" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Start-Process -FilePath $exePath -ArgumentList "--ui" -WorkingDirectory $PSScriptRoot
Start-Sleep -Seconds 2

Write-Host "✓ CloverBridge Testing UI ejecutándose" -ForegroundColor Green
Write-Host ""
Write-Host "Para detener: Get-Process CloverBridge | Stop-Process" -ForegroundColor Gray
Write-Host ""
