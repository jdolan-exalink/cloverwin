# CloverBridge - Versión C# .NET

Aplicación nativa de Windows para conectar ERP con terminales Clover POS usando Network Pay Display (SNPD).

## 🎯 Características

- ✅ **Ejecutable único portable** (~20-30MB, sin dependencias)
- ✅ **Windows Service** integrado (inicio automático con el sistema)
- ✅ **System Tray** con menú contextual
- ✅ **Ventana de Pairing** visual (WPF)
- ✅ **WebSocket nativo** a Clover
- ✅ **API HTTP** (puerto 3777)
- ✅ **File Watcher** para carpeta INBOX
- ✅ **Transaction Queue** FIFO
- ✅ **Logs con Serilog** (archivos diarios)

## 📦 Requisitos

- Windows 10/11 o Windows Server 2016+
- .NET 8.0 Runtime (incluido en ejecutable con --self-contained)

## 🚀 Instalación

### Modo 1: Aplicación de Tray (Recomendado)

```powershell
# Ejecutar el instalador o simplemente hacer doble clic en el .exe
.\CloverBridge.exe
```

La aplicación aparecerá en el System Tray. Al hacer doble clic, abre el dashboard web.

### Modo 2: Windows Service

```powershell
# Compilar
.\build.ps1

# Instalar como servicio (requiere administrador)
.\install-service.ps1

# Desinstalar
.\install-service.ps1 -Uninstall
```

### Modo 3: Consola (Para debugging)

```powershell
.\CloverBridge.exe --console
```

## 🔧 Compilación

```powershell
# Build Debug
.\build.ps1 -Configuration Debug

# Build Release (single-file executable)
.\build.ps1 -Configuration Release

# Build manual
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

El ejecutable se generará en: `.\bin\publish\CloverBridge.exe`

## ⚙️ Configuración

La configuración se almacena en:

```
C:\Users\[Usuario]\AppData\Roaming\CloverBridge\config.json
```

### Estructura de configuración:

```json
{
  "clover": {
    "host": "10.1.1.53",
    "port": 12345,
    "secure": false,
    "authToken": "",
    "remoteAppId": "clover-bridge",
    "posName": "ERP Bridge",
    "serialNumber": "CB-001"
  },
  "api": {
    "port": 3777,
    "host": "127.0.0.1"
  },
  "folders": {
    "inbox": "C:\\ProgramData\\CloverBridge\\INBOX",
    "outbox": "C:\\ProgramData\\CloverBridge\\OUTBOX",
    "archive": "C:\\ProgramData\\CloverBridge\\ARCHIVE"
  },
  "transaction": {
    "timeoutMs": 120000,
    "concurrency": 1
  }
}
```

## 📡 API Endpoints

Base URL: `http://localhost:3777`

### GET /api/health

Health check

### GET /api/status

Estado completo del sistema (Clover, queue, config)

### POST /api/transaction/sale

Iniciar venta

```json
{
  "amount": 1000,
  "externalId": "REQ-123",
  "note": "Venta de prueba"
}
```

### POST /api/transaction/void

Anular transacción

```json
{
  "originalTxId": "tx-id-here"
}
```

### POST /api/transaction/refund

Reembolso

```json
{
  "originalTxId": "tx-id-here",
  "amount": 500
}
```

### POST /api/qr

Mostrar QR Code

```json
{
  "amount": 2500,
  "externalId": "QR-123"
}
```

### POST /api/connect

Conectar a Clover (automático al iniciar)

### POST /api/disconnect

Desconectar de Clover

### GET /api/config

Obtener configuración

### POST /api/config

Actualizar configuración

## 📂 Carpetas de Datos

```
C:\ProgramData\CloverBridge\
├── INBOX\          # Requests del ERP (JSON)
├── OUTBOX\         # Responses (JSON)
└── ARCHIVE\        # Archivados
    ├── processed\  # Exitosos
    └── failed\     # Fallidos
```

## 📊 Logs

Los logs se guardan en:

```
C:\Users\[Usuario]\AppData\Roaming\CloverBridge\logs\
clover-bridge-YYYY-MM-DD.log
```

Ver logs en tiempo real:

```powershell
Get-Content "C:\Users\$env:USERNAME\AppData\Roaming\CloverBridge\logs\clover-bridge-*.log" -Tail 50 -Wait
```

## 🎨 System Tray

El icono en el System Tray muestra el estado de conexión:

- **Desconectado**: Sin conexión
- **Conectando...**: Intentando conectar
- **Pairing requerido**: Esperando código
- **Pareado**: Listo para transacciones

### Menú del Tray:

- **Abrir Dashboard**: Abre el navegador con la API
- **Mostrar Código de Pairing**: Muestra ventana con código PIN
- **Configuración**: Abre carpeta de configuración
- **Ver Logs**: Abre carpeta de logs
- **Salir**: Cierra la aplicación

## 🔐 Pairing con Clover

1. Iniciar CloverBridge
2. La ventana de pairing se abre automáticamente
3. En el terminal Clover:
   - Ir a **Configuración** → **Network Pay Display**
   - Habilitar SNPD
   - Ingresar el código de 6 dígitos
4. Confirmar en ambos dispositivos
5. La ventana se cierra automáticamente al completar

## 🐛 Troubleshooting

### El ejecutable no inicia

- Verificar que .NET 8.0 esté instalado (o usar versión self-contained)
- Revisar logs en `AppData\Roaming\CloverBridge\logs`

### No se conecta a Clover

- Verificar IP y puerto en config.json
- Verificar que SNPD esté habilitado en el terminal
- Revisar firewall de Windows

### El servicio no inicia

- Verificar permisos de administrador
- Revisar Event Viewer (Windows Logs → Application)
- Verificar que el puerto 3777 no esté en uso

### Puerto 3777 en uso

```powershell
# Ver qué proceso usa el puerto
Get-NetTCPConnection -LocalPort 3777 | Select-Object OwningProcess
Get-Process -Id [PID]

# Detener proceso
Stop-Process -Id [PID] -Force
```

## 🚀 Ventajas sobre Node.js/Electron

| Característica        | Node.js/Electron | C# .NET         |
| --------------------- | ---------------- | --------------- |
| Tamaño ejecutable     | ~120-150MB       | ~20-30MB        |
| Dependencias externas | Node, Electron   | Ninguna         |
| Tiempo de inicio      | 3-5 segundos     | <1 segundo      |
| Uso de memoria        | 150-200MB        | 40-60MB         |
| Windows Service       | Requiere wrapper | Nativo          |
| System Tray           | Electron API     | WinForms nativo |
| Compilación           | Complejo         | Simple          |
| Portable              | Problemático     | Nativo          |

## 📝 Arquitectura

```
CloverBridge.exe
├── Program.cs              # Entry point y modos de ejecución
├── Models/
│   ├── AppConfig.cs        # Configuración
│   └── CloverMessages.cs   # Mensajes Clover
├── Services/
│   ├── ConfigurationService.cs       # Gestión de config
│   ├── CloverWebSocketService.cs     # WebSocket a Clover
│   ├── ApiService.cs                 # HTTP API
│   ├── TransactionQueueService.cs    # Cola FIFO
│   └── InboxWatcherService.cs        # File watcher
└── UI/
    ├── TrayApplicationContext.cs     # System Tray
    ├── PairingWindow.xaml            # Ventana WPF
    └── PairingWindow.xaml.cs         # Code-behind
```

## 🔄 Flujo de Funcionamiento

1. **Inicio**: Se ejecuta Program.cs y detecta el modo
2. **Servicios**: Se inician todos los BackgroundService
3. **Conexión**: CloverWebSocketService conecta automáticamente
4. **Pairing**: Si no hay token, solicita pairing
5. **API**: ApiService escucha en puerto 3777
6. **Queue**: TransactionQueueService procesa cola FIFO
7. **Watcher**: InboxWatcherService monitorea carpeta INBOX
8. **Tray**: TrayApplicationContext muestra icono y menú

## 📞 Soporte

Para reportar bugs o solicitar features, crear un issue en el repositorio.

## 📄 Licencia

[Especificar licencia]
