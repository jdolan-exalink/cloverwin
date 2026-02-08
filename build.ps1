param(
    [string]$Configuration = "Release",
    [string]$Output = ".\dist"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CloverBridge - Build Script (x86)    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = Join-Path $PSScriptRoot "CloverBridge.csproj"

Write-Host "Building project..." -ForegroundColor Yellow
Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
Write-Host "   Runtime: win-x86 (32-bit)" -ForegroundColor Gray
Write-Host "   Output: $Output" -ForegroundColor Gray
Write-Host ""

# Limpiar carpeta dist anterior
if (Test-Path $Output) {
    Write-Host "Limpiando directorio anterior..." -ForegroundColor Yellow
    Remove-Item -Path $Output -Recurse -Force
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore $ProjectPath

Write-Host "Building and publishing (x86)..." -ForegroundColor Yellow
& dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime win-x86 `
    --self-contained true `
    --output $Output `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishReadyToRun=true

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Build completed successfully! (x86)  " -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Path: $Output" -ForegroundColor Gray
    Write-Host ""
    
    # Mostrar archivos generados
    if (Test-Path $Output) {
        Write-Host "Archivos generados:" -ForegroundColor Cyan
        Get-ChildItem $Output -Filter "*.exe" | ForEach-Object {
            $sizeMB = [math]::Round($_.Length / 1MB, 2)
            Write-Host "  - $($_.Name) ($sizeMB MB)" -ForegroundColor Gray
        }
    }
} else {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
