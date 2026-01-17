# CloverBridge - Test Rápido de Funcionalidad
# Verifica que todo esté funcionando correctamente

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$ExeDir = "D:\DEVs\Clover2\windows\bin\Debug\net8.0-windows"
$ExePath = Join-Path $ExeDir "CloverBridge.exe"

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  CloverBridge - Verificación de Instalación  ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$FailCount = 0
$PassCount = 0

function Test-Item-Status {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$SuccessMsg,
        [string]$FailMsg
    )
    
    Write-Host "  Testing: $Name... " -NoNewline
    
    if ($Condition) {
        Write-Host "✅ PASS" -ForegroundColor Green
        if ($Verbose -and $SuccessMsg) {
            Write-Host "    → $SuccessMsg" -ForegroundColor Gray
        }
        $script:PassCount++
        return $true
    } else {
        Write-Host "❌ FAIL" -ForegroundColor Red
        if ($FailMsg) {
            Write-Host "    → $FailMsg" -ForegroundColor Yellow
        }
        $script:FailCount++
        return $false
    }
}

# TEST 1: Ejecutable existe
Write-Host "🔍 Verificando archivos..." -ForegroundColor Yellow
Write-Host ""

Test-Item-Status `
    -Name "Ejecutable CloverBridge.exe" `
    -Condition (Test-Path $ExePath) `
    -SuccessMsg "Encontrado en $ExePath" `
    -FailMsg "No encontrado. Ejecutar 'dotnet build' primero"

if (Test-Path $ExePath) {
    $ExeInfo = Get-Item $ExePath
    Test-Item-Status `
        -Name "Tamaño del ejecutable" `
        -Condition ($ExeInfo.Length -gt 100KB) `
        -SuccessMsg "$([math]::Round($ExeInfo.Length/1KB, 2)) KB" `
        -FailMsg "Tamaño sospechosamente pequeño"
}

# TEST 2: Carpetas creadas
Write-Host ""
Write-Host "📁 Verificando estructura de carpetas..." -ForegroundColor Yellow
Write-Host ""

$Folders = @("INBOX", "OUTBOX", "ARCHIVE", "logs")
foreach ($Folder in $Folders) {
    $FolderPath = Join-Path $ExeDir $Folder
    Test-Item-Status `
        -Name "Carpeta $Folder" `
        -Condition (Test-Path $FolderPath -PathType Container) `
        -SuccessMsg "Creada en $FolderPath" `
        -FailMsg "No existe. La app debería crearla al iniciar"
}

# TEST 3: Config.json
Write-Host ""
Write-Host "⚙️  Verificando configuración..." -ForegroundColor Yellow
Write-Host ""

$ConfigPath = Join-Path $ExeDir "config.json"
$ConfigExists = Test-Item-Status `
    -Name "Archivo config.json" `
    -Condition (Test-Path $ConfigPath) `
    -SuccessMsg "Encontrado" `
    -FailMsg "No existe. La app debería crearlo al iniciar"

if ($ConfigExists) {
    try {
        $Config = Get-Content $ConfigPath | ConvertFrom-Json
        
        Test-Item-Status `
            -Name "Configuración válida JSON" `
            -Condition ($Config -ne $null) `
            -SuccessMsg "JSON parseado correctamente"
        
        Test-Item-Status `
            -Name "Sección 'clover' presente" `
            -Condition ($Config.clover -ne $null) `
            -SuccessMsg "Host: $($Config.clover.host), Port: $($Config.clover.port)"
        
        Test-Item-Status `
            -Name "Sección 'folders' presente" `
            -Condition ($Config.folders -ne $null) `
            -SuccessMsg "Inbox: $($Config.folders.inbox)"
        
        # Verificar que las rutas sean relativas al ejecutable
        if ($Config.folders.inbox -like "*$ExeDir*") {
            Test-Item-Status `
                -Name "Rutas apuntan al directorio ejecutable" `
                -Condition $true `
                -SuccessMsg "✅ Portabilidad correcta"
        } else {
            Test-Item-Status `
                -Name "Rutas apuntan al directorio ejecutable" `
                -Condition $false `
                -FailMsg "Rutas no son relativas al ejecutable"
        }
        
    } catch {
        Test-Item-Status `
            -Name "Configuración válida JSON" `
            -Condition $false `
            -FailMsg "Error parseando JSON: $_"
    }
}

# TEST 4: Logs
Write-Host ""
Write-Host "📋 Verificando logs..." -ForegroundColor Yellow
Write-Host ""

$LogsPath = Join-Path $ExeDir "logs"
if (Test-Path $LogsPath) {
    $LogFiles = Get-ChildItem $LogsPath -Filter "*.log" -ErrorAction SilentlyContinue
    
    Test-Item-Status `
        -Name "Archivos de log" `
        -Condition ($LogFiles.Count -gt 0) `
        -SuccessMsg "Encontrados $($LogFiles.Count) archivo(s) de log" `
        -FailMsg "No hay logs. La app debería crear logs al iniciar"
    
    if ($LogFiles.Count -gt 0 -and $Verbose) {
        $LatestLog = $LogFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Write-Host ""
        Write-Host "    📄 Log más reciente:" -ForegroundColor Cyan
        Write-Host "       Archivo: $($LatestLog.Name)" -ForegroundColor Gray
        Write-Host "       Tamaño: $($LatestLog.Length) bytes" -ForegroundColor Gray
        Write-Host "       Modificado: $($LatestLog.LastWriteTime)" -ForegroundColor Gray
        
        if ($LatestLog.Length -gt 0) {
            Write-Host ""
            Write-Host "    📝 Últimas 5 líneas:" -ForegroundColor Cyan
            Get-Content $LatestLog.FullName -Tail 5 | ForEach-Object {
                Write-Host "       $_" -ForegroundColor Gray
            }
        }
    }
}

# TEST 5: Proceso corriendo
Write-Host ""
Write-Host "🔄 Verificando procesos..." -ForegroundColor Yellow
Write-Host ""

$Process = Get-Process CloverBridge -ErrorAction SilentlyContinue
if ($Process) {
    Test-Item-Status `
        -Name "CloverBridge está corriendo" `
        -Condition $true `
        -SuccessMsg "PID: $($Process.Id), Memoria: $([math]::Round($Process.WorkingSet64/1MB, 2)) MB"
    
    Write-Host ""
    Write-Host "    ℹ️  Para detener: Get-Process CloverBridge | Stop-Process -Force" -ForegroundColor Cyan
} else {
    Test-Item-Status `
        -Name "CloverBridge está corriendo" `
        -Condition $false `
        -FailMsg "No hay proceso corriendo (esto es normal si no lo iniciaste)"
}

# TEST 6: .NET Runtime
Write-Host ""
Write-Host "🔧 Verificando dependencias..." -ForegroundColor Yellow
Write-Host ""

try {
    $DotNetVersion = & dotnet --version 2>$null
    Test-Item-Status `
        -Name ".NET SDK instalado" `
        -Condition ($LASTEXITCODE -eq 0) `
        -SuccessMsg "Versión: $DotNetVersion"
} catch {
    Test-Item-Status `
        -Name ".NET SDK instalado" `
        -Condition $false `
        -FailMsg "dotnet comando no encontrado"
}

# RESUMEN FINAL
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  📊 RESUMEN DE TESTS:" -ForegroundColor White
Write-Host ""
Write-Host "    ✅ Passed: " -NoNewline -ForegroundColor Green
Write-Host "$PassCount" -ForegroundColor White
Write-Host "    ❌ Failed: " -NoNewline -ForegroundColor Red
Write-Host "$FailCount" -ForegroundColor White
Write-Host ""

if ($FailCount -eq 0) {
    Write-Host "  🎉 TODO CORRECTO - La aplicación está lista para usar" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Ejecutar con:" -ForegroundColor Cyan
    Write-Host "    .\bin\Debug\net8.0-windows\CloverBridge.exe --ui" -ForegroundColor White
} elseif ($FailCount -le 2) {
    Write-Host "  ⚠️  ALGUNOS PROBLEMAS MENORES - Revisar logs" -ForegroundColor Yellow
} else {
    Write-Host "  ❌ MÚLTIPLES PROBLEMAS - Verificar instalación" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Sugerencias:" -ForegroundColor Cyan
    Write-Host "    1. Compilar el proyecto: dotnet build" -ForegroundColor White
    Write-Host "    2. Ejecutar una vez para crear archivos: .\bin\Debug\net8.0-windows\CloverBridge.exe --ui" -ForegroundColor White
    Write-Host "    3. Volver a ejecutar este test" -ForegroundColor White
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

exit $FailCount
