# Script para ejecutar prueba de OUTBOX
Write-Host "=== Ejecutando prueba de lectura OUTBOX ===" -ForegroundColor Cyan
Write-Host ""

# Ruta al ejecutable
$exePath = "bin\Debug\net8.0-windows\CloverBridge.exe"

if (Test-Path $exePath) {
    & $exePath --test-outbox
} else {
    Write-Host "Error: No se encontró el ejecutable en $exePath" -ForegroundColor Red
    Write-Host "Compilando primero..." -ForegroundColor Yellow
    dotnet build Cloverwin.sln --configuration Debug
    
    if (Test-Path $exePath) {
        Write-Host "Ejecutando prueba..." -ForegroundColor Green
        & $exePath --test-outbox
    } else {
        Write-Host "No se pudo compilar correctamente" -ForegroundColor Red
    }
}
