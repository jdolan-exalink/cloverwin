# Build CloverBridge for Release
# Creates portable executable
# Run as: .\build-release.ps1

param(
    [switch]$Clean = $false
)

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   CloverBridge - Release Build" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Detener procesos
Write-Host "[1/5] Deteniendo procesos..." -ForegroundColor Yellow
Get-Process -Name "CloverBridge" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Limpiar si se solicita
if ($Clean) {
    Write-Host "[2/5] Limpiando build anterior..." -ForegroundColor Yellow
    Remove-Item -Path ".\bin" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path ".\obj" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "✓ Build anterior limpiado" -ForegroundColor Green
} else {
    Write-Host "[2/5] Manteniendo build anterior" -ForegroundColor Gray
}

# Restaurar dependencias
Write-Host "[3/5] Restaurando dependencias..." -ForegroundColor Yellow
dotnet restore CloverBridge.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Error restaurando dependencias" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Dependencias restauradas" -ForegroundColor Green

# Compilar Release
Write-Host "[4/5] Compilando Release..." -ForegroundColor Yellow
dotnet publish CloverBridge.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Error en compilación Release" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Compilación Release exitosa" -ForegroundColor Green

# Verificar resultado
$releasePath = ".\bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe"

if (Test-Path $releasePath) {
    $fileInfo = Get-Item $releasePath
    $sizeInMB = [math]::Round($fileInfo.Length / 1MB, 2)
    
    Write-Host "[5/5] ✓ Build completado exitosamente" -ForegroundColor Green
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "   Ejecutable generado:" -ForegroundColor Green
    Write-Host "   $releasePath" -ForegroundColor White
    Write-Host "   Tamaño: $sizeInMB MB" -ForegroundColor Gray
    Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "MODOS DE EJECUCIÓN:" -ForegroundColor Yellow
    Write-Host "  CloverBridge.exe              → Modo bandeja (default)" -ForegroundColor White
    Write-Host "  CloverBridge.exe --ui         → Testing Dashboard UI" -ForegroundColor White
    Write-Host "  CloverBridge.exe --console    → Modo consola" -ForegroundColor White
    Write-Host "  CloverBridge.exe --service    → Windows Service" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "[5/5] ✗ No se encontró el ejecutable" -ForegroundColor Red
    exit 1
}
