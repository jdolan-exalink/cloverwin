# CloverBridge Windows - Quick Reference

## 🚀 Inicio Rápido

### Primera vez (desarrollo)

```powershell
cd windows
.\start.ps1
```

### Build para producción

```powershell
.\build.ps1
```

### Instalar como servicio

```powershell
.\build.ps1
.\install-service.ps1
```

## 📋 Estructura de Archivos

```
windows/
├── CloverBridge.csproj         # Proyecto .NET
├── Program.cs                  # Entry point
├── appsettings.json            # Config por defecto
├── Models/                     # Modelos de datos
│   ├── AppConfig.cs
│   └── CloverMessages.cs
├── Services/                   # Servicios backend
│   ├── ConfigurationService.cs
│   ├── CloverWebSocketService.cs
│   ├── ApiService.cs
│   ├── TransactionQueueService.cs
│   └── InboxWatcherService.cs
├── UI/                         # Interfaz gráfica
│   ├── TrayApplicationContext.cs
│   ├── PairingWindow.xaml
│   └── PairingWindow.xaml.cs
├── build.ps1                   # Script de compilación
├── install-service.ps1         # Instalador de servicio
└── start.ps1                   # Inicio rápido dev
```

## 🎯 Modos de Ejecución

### 1. Modo Tray (Normal)

```powershell
.\CloverBridge.exe
```

- Aparece en System Tray
- Ventana de pairing automática
- Menú contextual

### 2. Modo Consola (Debug)

```powershell
.\CloverBridge.exe --console
```

- Output visible en consola
- Logs en tiempo real
- Ctrl+C para detener

### 3. Modo Servicio

```powershell
.\CloverBridge.exe --service
```

- Ejecutado por Windows Service Manager
- No usar manualmente

## 🔧 Comandos Útiles

### Development

```powershell
# Compilar
dotnet build

# Ejecutar (debug)
dotnet run -- --console

# Limpiar
dotnet clean

# Restaurar paquetes
dotnet restore

# Publicar single-file
dotnet publish -c Release -r win-x64 --self-contained
```

### Testing API

```powershell
# Health check
Invoke-RestMethod http://localhost:3777/api/health

# Status
Invoke-RestMethod http://localhost:3777/api/status

# Venta
$body = @{ amount = 1000; externalId = "TEST-001" } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:3777/api/transaction/sale -Method POST -Body $body -ContentType "application/json"
```

### Service Management

```powershell
# Estado
Get-Service CloverBridge

# Iniciar
Start-Service CloverBridge

# Detener
Stop-Service CloverBridge

# Reiniciar
Restart-Service CloverBridge

# Ver logs del servicio
Get-EventLog -LogName Application -Source CloverBridge -Newest 50
```

### Logs

```powershell
# Ver logs en tiempo real
$logPath = "$env:APPDATA\CloverBridge\logs\clover-bridge-$(Get-Date -Format yyyy-MM-dd).log"
Get-Content $logPath -Tail 50 -Wait

# Abrir carpeta de logs
explorer "$env:APPDATA\CloverBridge\logs"
```

### Configuration

```powershell
# Ver configuración
Get-Content "$env:APPDATA\CloverBridge\config.json" | ConvertFrom-Json

# Editar configuración
notepad "$env:APPDATA\CloverBridge\config.json"

# Abrir carpeta de configuración
explorer "$env:APPDATA\CloverBridge"
```

## 🐛 Troubleshooting

### Puerto 3777 en uso

```powershell
# Ver proceso
Get-NetTCPConnection -LocalPort 3777 -ErrorAction SilentlyContinue

# Detener proceso
$pid = (Get-NetTCPConnection -LocalPort 3777).OwningProcess
Stop-Process -Id $pid -Force
```

### Reinstalar servicio

```powershell
.\install-service.ps1 -Uninstall
.\install-service.ps1
```

### Limpiar todo

```powershell
# Detener y desinstalar servicio
Stop-Service CloverBridge -ErrorAction SilentlyContinue
sc.exe delete CloverBridge

# Eliminar configuración
Remove-Item "$env:APPDATA\CloverBridge" -Recurse -Force

# Eliminar datos
Remove-Item "C:\ProgramData\CloverBridge" -Recurse -Force

# Limpiar build
dotnet clean
Remove-Item "bin" -Recurse -Force
Remove-Item "obj" -Recurse -Force
```

## 📦 Dependencias NuGet

- Microsoft.Extensions.Hosting (8.0.0)
- Microsoft.Extensions.Hosting.WindowsServices (8.0.0)
- Microsoft.Extensions.Configuration.Json (8.0.0)
- Serilog (3.1.1)
- Serilog.Extensions.Hosting (8.0.0)
- Serilog.Sinks.File (5.0.0)
- Serilog.Sinks.Console (5.0.1)
- System.Text.Json (8.0.4)

## 🎨 Ventana de Pairing

La ventana se abre automáticamente cuando:

- Se requiere pairing inicial
- Se hace clic en "Mostrar Código de Pairing" en el tray

Características:

- Código PIN grande y visible
- Instrucciones claras
- Se cierra automáticamente al completar
- Estilo dark mode

## 📊 API Endpoints

| Endpoint                  | Método | Descripción       |
| ------------------------- | ------ | ----------------- |
| `/api/health`             | GET    | Health check      |
| `/api/status`             | GET    | Estado completo   |
| `/api/connect`            | POST   | Conectar a Clover |
| `/api/disconnect`         | POST   | Desconectar       |
| `/api/config`             | GET    | Obtener config    |
| `/api/config`             | POST   | Actualizar config |
| `/api/transaction/sale`   | POST   | Venta             |
| `/api/transaction/void`   | POST   | Anular            |
| `/api/transaction/refund` | POST   | Reembolso         |
| `/api/qr`                 | POST   | QR Code           |

## ✨ Features Implementadas

- ✅ WebSocket a Clover (reconnect automático)
- ✅ Pairing visual con código PIN
- ✅ API HTTP con todos los endpoints
- ✅ Transaction Queue FIFO
- ✅ File Watcher (INBOX)
- ✅ Atomic writes (OUTBOX)
- ✅ Archivado automático
- ✅ System Tray con menú
- ✅ Windows Service support
- ✅ Logs rotativos con Serilog
- ✅ Configuración persistente
- ✅ Ejecutable single-file portable
- ✅ CORS habilitado en API
- ✅ Timeouts configurables
- ✅ Manejo de errores robusto
