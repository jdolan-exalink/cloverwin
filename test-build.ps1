param([int]$Timeout = 5)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishPath = Join-Path $scriptRoot "bin\Release\net8.0-windows\win-x64\publish"
$exePath = Join-Path $publishPath "CloverBridge.exe"

Write-Host "== CloverBridge Build Test ==" -ForegroundColor Cyan

# Build Release
Write-Host "`nCompilando Release..." -ForegroundColor Yellow
Set-Location $scriptRoot
dotnet build Cloverwin.sln -c Release 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK - Compilacion exitosa" -ForegroundColor Green
} else {
    Write-Host "ERROR - Compilacion fallida" -ForegroundColor Red
    exit 1
}

# Publish
Write-Host "Publicando ejecutable..." -ForegroundColor Yellow
dotnet publish Cloverwin.sln -c Release 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK - Publicacion exitosa" -ForegroundColor Green
} else {
    Write-Host "ERROR - Publicacion fallida" -ForegroundColor Red
    exit 1
}

# Verify executable
Write-Host "Verificando ejecutable..." -ForegroundColor Yellow
if (Test-Path $exePath) {
    $size = [math]::Round((Get-Item $exePath).Length / 1MB)
    Write-Host "OK - CloverBridge.exe creado (~${size}MB)" -ForegroundColor Green
} else {
    Write-Host "ERROR - Ejecutable no encontrado" -ForegroundColor Red
    exit 1
}

# Test execution
Write-Host "Ejecutando en consola (${Timeout}s)..." -ForegroundColor Yellow
Set-Location $publishPath
$proc = Start-Process -FilePath $exePath -ArgumentList "--console" -NoNewWindow -PassThru `
    -RedirectStandardOutput "test-out.log" -RedirectStandardError "test-err.log"

Start-Sleep -Seconds $Timeout
if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

$out = Get-Content "test-out.log" -ErrorAction SilentlyContinue
if ($out -match "CloverBridge starting") {
    Write-Host "OK - Aplicacion inicia correctamente" -ForegroundColor Green
}

Write-Host "`n== RESULTADO: LISTO PARA USAR ==" -ForegroundColor Green
Write-Host "`nEjecutar desde: $publishPath" -ForegroundColor Gray
Write-Host "  .\CloverBridge.exe              (UI Tray)" -ForegroundColor Gray
Write-Host "  .\CloverBridge.exe --console   (Modo Debug)" -ForegroundColor Gray

Remove-Item "test-out.log" -ErrorAction SilentlyContinue
Remove-Item "test-err.log" -ErrorAction SilentlyContinue
