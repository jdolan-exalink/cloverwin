# CloverBridge - Sistema de Pagos v1.0

Aplicación multi-arquitectura de Windows para conectar sistemas ERP con terminales Clover POS mediante WebSocket Network Pay Display (SNPD).

## 📋 Tabla de Contenidos

- [Características](#características)
- [Requisitos](#requisitos)
- [Instalación](#instalación)
- [Configuración](#configuración)
- [Modos de Ejecución](#modos-de-ejecución)
- [Troubleshooting](#troubleshooting)
- [Licencia](#licencia)

## ✨ Características

### 🔌 Conectividad
- **WebSocket Clover**: Comunicación bidireccional nativa con terminales Clover
- **Múltiples modos**: Sistema Tray, Windows Service, Consola, Dashboard UI
- **Single Instance Control**: Solo una instancia puede ejecutarse simultáneamente
- **Reconexión automática**: Reintentos configurables con backoff exponencial

### 📊 Gestión de Transacciones
- **Transaction Queue**: Sistema FIFO con timeout de 120 segundos
- **File Watcher**: Monitoreo de carpeta INBOX para transacciones entrantes
- **Organización**: Carpetas INBOX, OUTBOX, ARCHIVE
- **Logging completo**: Serilog con rotación diaria (máximo 30 días)

### 🎯 Interfaz de Usuario
- **System Tray**: Icono dinámico con emoji 💳
- **Dashboard WPF**: ProductionMainWindow optimizado para 1280x720
- **Testing UI**: MainWindow compacto para 1100x680
- **Pairing Window**: Configuración visual de dispositivos

### 🏗️ Arquitectura
- **.NET 8.0**: Framework moderno con C# 12
- **Windows Forms + WPF**: Interfaz híbrida nativa
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async/Await**: Operaciones no-bloqueantes
- **Self-Contained**: Ejecutables portátiles sin dependencias

### 🔐 Seguridad
- **Mutex Single Instance**: Previene duplicación de procesos
- **HTTPS/WSS**: Soporte para conexiones seguras
- **Logging sin PII**: Datos sensibles no se registran
- **Ejecutables firmados**: Identidad verificable

## 📦 Requisitos

### Sistema Operativo
- **x86**: Windows 7 SP1, Windows 8, Windows 8.1, Windows 10 (32-bit)
- **x64**: Windows 10, Windows 11 (64-bit)
- **.NET 8.0 Runtime**: Incluido en ejecutable self-contained

### Hardware
- **CPU**: Intel/AMD compatible con conjunto de instrucciones básico
- **RAM**: 256 MB mínimo (512 MB recomendado)
- **Almacenamiento**: 100 MB disponible

### Red
- **Conectividad**: Acceso a red donde se encuentra terminal Clover
- **Puertos**: 12345 (Clover), 3777 (API HTTP)

## 🚀 Instalación

### Opción 1: Aplicación Tray (Recomendado)

1. Descarga el ejecutable apropiado:
   - `CloverBridge-x64.exe` para Windows 10/11 64-bit
   - `CloverBridge-x86.exe` para Windows 7 32-bit

2. Haz doble clic para ejecutar

3. Configurar en:
   - `appsettings.json` (valores por defecto)
   - `config.json` (creado automáticamente en primera ejecución)

### Opción 2: Windows Service

```powershell
# Ejecutar como Administrador
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\install-service.ps1

# Verificar
Get-Service CloverBridge
Start-Service CloverBridge

# Desinstalar
.\install-service.ps1 -Uninstall
```

### Opción 3: Consola (Debugging)

```powershell
.\CloverBridge.exe --console
```

### Opción 4: Dashboard Completo

```powershell
.\CloverBridge.exe --ui
```

## ⚙️ Configuración

### Archivo: appsettings.json

```json
{
  "Clover": {
    "Host": "10.1.1.53",
    "Port": 12345,
    "Secure": false,
    "RemoteAppId": "clover-bridge",
    "PosName": "ERP Bridge",
    "SerialNumber": "CB-001",
    "ReconnectDelayMs": 5000,
    "MaxReconnectAttempts": 10
  },
  "Api": {
    "Port": 3777,
    "Host": "127.0.0.1"
  },
  "Folders": {
    "Inbox": "C:\\ProgramData\\CloverBridge\\INBOX",
    "Outbox": "C:\\ProgramData\\CloverBridge\\OUTBOX",
    "Archive": "C:\\ProgramData\\CloverBridge\\ARCHIVE"
  },
  "Transaction": {
    "TimeoutMs": 120000,
    "Concurrency": 1
  }
}
```

### Parámetros Clave

| Parámetro | Descripción | Default |
|-----------|-------------|----------|
| `Host` | IP de terminal Clover | 10.1.1.53 |
| `Port` | Puerto WebSocket Clover | 12345 |
| `Secure` | Usar WSS (HTTPS) | false |
| `RemoteAppId` | ID de aplicación remota | clover-bridge |
| `ReconnectDelayMs` | Espera entre reintentos | 5000 |
| `MaxReconnectAttempts` | Máximo de intentos | 10 |
| `Api.Port` | Puerto HTTP para API | 3777 |

## 🎯 Modos de Ejecución

### Sistema Tray (Default)

```powershell
.\CloverBridge.exe
```

**Características:**
- Icono dinámico en system tray
- Menú contextual con opciones
- Logs en carpeta `logs/`
- Única instancia permitida

### Windows Service

```powershell
.\install-service.ps1
```

**Características:**
- Inicia automáticamente con Windows
- Se ejecuta con privilegios de servicio
- Logs en Event Viewer
- Control vía Services.msc

### Consola

```powershell
.\CloverBridge.exe --console
```

**Características:**
- Logs en tiempo real en pantalla
- Ideal para debugging
- Presionar Ctrl+C para salir
- Logs también en archivo

### Dashboard UI

```powershell
.\CloverBridge.exe --ui
```

**Características:**
- Interfaz WPF completa
- Panel de control visual
- Testing de transacciones
- Monitoreo en tiempo real

## 📊 API HTTP

La aplicación expone API REST en `http://127.0.0.1:3777`:

### Health Check

```bash
GET /health

Respuesta:
{
  "status": "ok",
  "timestamp": "2026-01-16T12:00:00Z"
}
```

### Sistema Status

```bash
GET /status

Respuesta:
{
  "cloverConnected": true,
  "queueSize": 5,
  "lastTransactionTime": "2026-01-16T11:59:30Z",
  "uptime": "02:30:45"
}
```

## 🔌 WebSocket Protocol

### Conexión

```
wss://[Host]:[Port]/remote_pay
```

### Ejemplo: Solicitud de Pago

```json
{
  "type": "PAY_INTENT",
  "externalId": "ERP-001-20260116",
  "amount": 5000,
  "tipAmount": 0,
  "currency": "USD"
}
```

### Respuesta

```json
{
  "type": "SALE",
  "transactionId": "TXN-12345",
  "status": "completed",
  "amount": 5000,
  "timestamp": "2026-01-16T12:00:00Z"
}
```

## 🐛 Troubleshooting

### "CloverBridge ya se está ejecutando"

**Causa**: Solo una instancia puede ejecutarse

**Solución**:
```powershell
# Cerrar instancia anterior desde system tray
# O matar proceso:
Get-Process CloverBridge | Stop-Process -Force

# Luego ejecutar de nuevo
.\CloverBridge.exe
```

### No conecta a terminal Clover

**Verificaciones**:
1. Host y Port correctos en `appsettings.json`
2. Conectividad: `ping [host]`
3. Puerto disponible: `netstat -an | findstr :12345`
4. Revisar logs: `logs/clover-bridge-*.log`

### Puertos ocupados

```powershell
# Encontrar proceso en puerto 3777
netstat -ano | findstr :3777

# Encontrar proceso en puerto 12345
netstat -ano | findstr :12345

# Matar proceso (si es necesario)
Stop-Process -Id [PID] -Force
```

### Logs no se generan

```powershell
# Verificar carpeta logs
Get-ChildItem .\logs\

# Permisos de escritura
icacls .\logs /grant:r $($env:USERNAME):(OI)(CI)F /T
```

## 📝 Logs

Archivos de log se generan en `logs/` con rotación diaria:

```
logs/
├── clover-bridge-20260116.log
├── clover-bridge-20260115.log
└── clover-bridge-20260114.log
```

**Formato**:
```
2026-01-16 12:00:00 [INF] CloverBridge starting...
2026-01-16 12:00:01 [INF] WebSocket connected to 10.1.1.53:12345
2026-01-16 12:05:23 [INF] Transaction TXN-001 completed
```

**Retención**: Máximo 30 días

## 🔧 Compilación desde Código Fuente

### Requisitos
- .NET 8.0 SDK
- Visual Studio 2022 (o VS Code + C# extension)
- Windows 10+

### Build

```powershell
# Debug
dotnet build Cloverwin.sln

# Release x64
dotnet publish -c Release -r win-x64 --self-contained

# Release x86
dotnet publish -c Release -r win-x86 --self-contained
```

### Ubicación de salida

```
bin/Release/net8.0-windows/
├── win-x64/publish/CloverBridge.exe (73.67 MB)
└── win-x86/publish/CloverBridge.exe (67.15 MB)
```

## 📊 Estadísticas de Compilación

- **Errores de compilación**: 0
- **Warnings**: 0
- **Métodos**: 150+
- **Clases**: 25+
- **Líneas de código**: 5000+
- **Tiempo de compilación**: <30 segundos

## 🔐 Seguridad

- Ejecutables completamente self-contained
- Sin DLLs externas vulnerables
- Logging sin datos sensibles
- Single Instance Mutex previene ataques
- Soporte HTTPS/WSS para comunicación segura

## 📄 Licencia

MIT License - Libre para uso comercial y personal

## 👥 Contribuciones

Las contribuciones son bienvenidas. Por favor:
1. Fork el repositorio
2. Crea una rama para tu feature
3. Commit tus cambios
4. Push a la rama
5. Abre un Pull Request

## 🆘 Soporte

Para reportar bugs o solicitar features:
- Abre un [Issue](https://github.com/jdolan-exalink/cloverwin/issues)
- Incluye logs relevantes
- Describe el entorno (Windows version, etc.)

## 📈 Versión

**Actual**: v1.0.0
**Fecha**: 16 Enero 2026
**Status**: Producción

---

**CloverBridge** © 2026. Todos los derechos reservados.
