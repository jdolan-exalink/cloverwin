# Script para copiar archivos de ejemplo a OUTBOX y probar la lectura
# Ejecutar desde el directorio raíz del proyecto

Write-Host "=== Preparando archivos de prueba para OUTBOX ===" -ForegroundColor Cyan

# Rutas
$sourceDir = "bin\Release\net8.0-windows\win-x64\OUTBOX"
$targetDir = "bin\Debug\net8.0-windows\OUTBOX"

# Crear directorio OUTBOX si no existe
if (-not (Test-Path $targetDir)) {
    Write-Host "Creando directorio OUTBOX: $targetDir" -ForegroundColor Yellow
    New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
}

# Verificar si hay archivos de ejemplo en el source
if (Test-Path $sourceDir) {
    $files = Get-ChildItem -Path $sourceDir -Filter "*.json"
    
    if ($files.Count -gt 0) {
        Write-Host "`nEncontrados $($files.Count) archivos de ejemplo en $sourceDir" -ForegroundColor Green
        
        foreach ($file in $files) {
            Write-Host "  Copiando: $($file.Name)" -ForegroundColor Gray
            Copy-Item -Path $file.FullName -Destination $targetDir -Force
        }
        
        Write-Host "`nArchivos copiados exitosamente a $targetDir" -ForegroundColor Green
    } else {
        Write-Host "`nNo se encontraron archivos JSON en $sourceDir" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nDirectorio fuente no encontrado: $sourceDir" -ForegroundColor Yellow
}

# Listar archivos en OUTBOX de debug
Write-Host "`n=== Archivos en OUTBOX de Debug ===" -ForegroundColor Cyan
if (Test-Path $targetDir) {
    $debugFiles = Get-ChildItem -Path $targetDir -Filter "*.json" | Sort-Object Name
    
    if ($debugFiles.Count -gt 0) {
        foreach ($file in $debugFiles) {
            $size = [math]::Round($file.Length / 1KB, 2)
            Write-Host "  $($file.Name) - ${size}KB - $($file.LastWriteTime)" -ForegroundColor White
        }
        Write-Host "`nTotal: $($debugFiles.Count) archivos" -ForegroundColor Green
    } else {
        Write-Host "  No hay archivos en OUTBOX" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Directorio no existe: $targetDir" -ForegroundColor Red
}

Write-Host "`n=== Listo para pruebas ===" -ForegroundColor Green
Write-Host "Puedes probar ahora con TestOutboxReader.TestOutboxAsync()" -ForegroundColor Cyan
