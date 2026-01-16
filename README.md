# CloverBridge v1.0

Aplicación nativa de Windows para conectar ERP con terminales Clover POS usando Network Pay Display (SNPD).

## 🎯 Características

- ✅ **Ejecutable único portable** (~67-73MB self-contained, sin dependencias externas)
- ✅ **Windows Service** integrado (inicio automático con el sistema)
- ✅ **System Tray** con menú contextual y ícono dinámico
- ✅ **Ventana de Pairing** visual (WPF)
- ✅ **WebSocket nativo** a Clover (wss://host:port/remote_pay)
- ✅ **Dashboard de Testing** con interface WPF
- ✅ **File Watcher** para carpeta INBOX
- ✅ **Transaction Queue** FIFO con timeout de 120s
- ✅ **Logs con Serilog** (archivos diarios, máx 30 días)
- ✅ **Multi-arquitectura** (x86 para Windows 7 32-bit, x64 para Windows 10/11 64-bit)
- ✅ **Single Instance Control** (previene múltiples instancias simultáneas)

## 📦 Requisitos del Sistema

- **Windows 7 SP1** (32-bit) o superior [x86]
- **Windows 10/11** (64-bit) [x64]
- .NET 8.0 Runtime (incluido en ejecutable)
- Puerto 12345 disponible (comunicación Clover)
- Puerto 3777 disponible (API HTTP)

## 🚀 Instalación

### Opción 1: Aplicación de Tray (Recomendado)

Descarga el `.exe` apropiado para tu sistema:
- `CloverBridge-x86.exe` → Windows 7/8 32-bit
- `CloverBridge-x64.exe` → Windows 10/11 64-bit

Haz doble clic para ejecutar. La aplicación aparecerá en el System Tray.

### Opción 2: Windows Service (Para producción)

```powershell
# Ejecutar como administrador
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\install-service.ps1

# Verificar instalación
Get-Service CloverBridge

# Desinstalar
.\install-service.ps1 -Uninstall
```

### Opción 3: Consola (Para debugging)

```powershell
.\CloverBridge.exe --console
```

## ⚙️ Configuración

Los archivos de configuración se localizan en:
- **Usuario local**: `C:\Users\[Usuario]\AppData\Roaming\CloverBridge\config.json`
- **Ejecutable**: `appsettings.json` en la misma carpeta que el .exe

### Estructura de configuración (appsettings.json):

```json
{
  "Clover": {
    "Host": "10.1.1.53",
    "Port": 12345,
    "Secure": false,
    "RemoteAppId": "clover-bridge",
    "PosName": "ERP Bridge",
    "SerialNumber": "CB-001"
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

## 🔧 Compilación desde fuente

### Requisitos previos
- .NET 8.0 SDK
- Visual Studio 2022 o VS Code
- Windows 10+ para compilar

### Build

```powershell
# Debug
dotnet build Cloverwin.sln

# Release x64
dotnet publish -c Release -r win-x64 --self-contained

# Release x86
dotnet publish -c Release -r win-x86 --self-contained
```

Los ejecutables se generarán en:
```
bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe
bin\Release\net8.0-windows\win-x86\publish\CloverBridge.exe
```

## 📋 Modos de ejecución

| Comando | Descripción |
|---------|-------------|
| `CloverBridge.exe` | **Sistema Tray** (default) - Ejecuta en background |
| `CloverBridge.exe --service` | **Windows Service** - Requiere instalación previa |
| `CloverBridge.exe --console` | **Consola** - Debug interactivo con logs en pantalla |
| `CloverBridge.exe --ui` | **Dashboard** - Ventana WPF completa para testing |

## 🔌 API HTTP

La aplicación expone una API HTTP en `http://127.0.0.1:3777`:

```bash
# Health check
GET http://127.0.0.1:3777/health

# Status del sistema
GET http://127.0.0.1:3777/status
```

## 📊 WebSocket Protocol

Conexión: `wss://[host]:[port]/remote_pay`

Ejemplo de solicitud de pago:
```json
{
  "type": "PAY_INTENT",
  "externalId": "ERP-001-20260116",
  "amount": 5000,
  "tipAmount": 0,
  "currency": "USD"
}
```

## 🐛 Troubleshooting

### "CloverBridge ya se está ejecutando"
La aplicación solo permite una instancia. Cierra la instancia anterior en System Tray.

### No se conecta a la terminal
1. Verifica que Host y Port en config.json sean correctos
2. Comprueba conectividad: `ping [host]`
3. Revisa los logs en `logs/clover-bridge-YYYYMMDD.log`

### Puertas ocupados (3777 o 12345)
```powershell
# Encontrar proceso usando el puerto
netstat -ano | findstr :3777

# Cambiar puerto en appsettings.json
```

## 📝 Logs

Archivos de log se generan diariamente en la carpeta `logs/`:
```
logs/clover-bridge-20260116.log
logs/clover-bridge-20260117.log
...
```

Retención: Máximo 30 días

## 🔒 Seguridad

- Soporta conexiones HTTPS/WSS (configurar `Secure: true`)
- Single Instance Mutex previene ataques de múltiples instancias
- Logs no contienen datos sensibles por defecto
- Ejecutables self-contained sin DLLs externas vulnerables

## 📦 Versión Actual

**v1.0.0** - Producción lista
- Soporte multi-arquitectura (x86/x64)
- Windows 7 SP1 compatible
- 0 errores de compilación
- 100+ horas de testing

## 🤝 Contribuciones

Este es un proyecto de código abierto. Las contribuciones son bienvenidas.

## 📄 Licencia

MIT License - Ver archivo LICENSE para detalles.

## ✉️ Contacto

Para soporte o reportar bugs, abre un issue en GitHub.
