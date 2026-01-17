# Launch CloverBridge in Tray Mode (default)
# Run as: .\launch-tray.ps1

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   CloverBridge - Tray Mode Launcher" -ForegroundColor Green
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

# Lanzar aplicación en modo bandeja
Write-Host "[3/3] Iniciando en modo bandeja..." -ForegroundColor Yellow
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   CloverBridge ejecutándose en bandeja" -ForegroundColor Green
Write-Host "   Haz doble clic en el ícono para abrir UI" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Start-Process -FilePath $exePath -WorkingDirectory $PSScriptRoot
Start-Sleep -Seconds 2

Write-Host "✓ CloverBridge ejecutándose en bandeja del sistema" -ForegroundColor Green
Write-Host ""
Write-Host "Busca el ícono en la bandeja del sistema (system tray)" -ForegroundColor Cyan
Write-Host ""
