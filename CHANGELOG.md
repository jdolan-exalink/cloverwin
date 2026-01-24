# Cambios en v1.0.0

## Características principales

### ✨ Funcionalidad Core
- **WebSocket Clover**: Comunicación bidireccional con terminales Clover
- **Múltiples modos de ejecución**: Tray, Service, Console, UI
- **Single Instance Control**: Previene múltiples instancias simultáneas
- **Logging completo**: Serilog con rotación diaria
- **Transacciones FIFO**: Queue con timeout configurable

### 🏗️ Arquitectura
- **.NET 8.0** multi-target (Windows Forms + WPF)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async/await**: Operaciones no-bloqueantes
- **Self-contained**: Ejecutables portátiles

### 🖥️ Interfaz de Usuario
- **System Tray**: Icono dinámico con emoji (💳)
- **WPF Dashboard**: ProductionMainWindow (1280x720)
- **Testing UI**: MainWindow para desarrollo (1100x680)
- **Pairing Window**: Configuración visual de dispositivos

### 🏪 Multi-Arquitectura
- **x86**: 67.15 MB (Windows 7 SP1 32-bit)
- **x64**: 73.67 MB (Windows 10/11 64-bit)
- **Single-file**: Ejecutables completamente contenidos
- **Comprimidos**: Compresión habilitada en release

### 🔐 Seguridad
- **Mutex-based Single Instance**: Previene acceso concurrente
- **HTTPS/WSS support**: Conexiones seguras configurables
- **Logging sin datos sensibles**: PII no se registra

### 📊 Monitoring
- **API HTTP**: Health check y status endpoints
- **Logs diarios**: Retención de 30 días
- **Event logging**: Todos los eventos importantes registrados

## Compilación

```powershell
# Build Debug
dotnet build Cloverwin.sln

# Publish x64
dotnet publish -c Release -r win-x64 --self-contained

# Publish x86  
dotnet publish -c Release -r win-x86 --self-contained
```

## Instalación

### Tray Mode (Default)
```powershell
.\CloverBridge.exe
```

### Windows Service
```powershell
.\install-service.ps1
```

### Console Mode
```powershell
.\CloverBridge.exe --console
```

## Configuración

- **appsettings.json**: Configuración por defecto
- **config.json**: Configuración de usuario (se crea automáticamente)
- **Carpetas**: INBOX, OUTBOX, ARCHIVE en la carpeta del ejecutable

## Requisitos del Sistema

- **x86**: Windows 7 SP1 o superior (32-bit)
- **x64**: Windows 10 o superior (64-bit)
- **.NET 8.0 Runtime**: Incluido en ejecutable
- **Puertos**: 12345 (Clover) y 3777 (API) disponibles

## Modos de Ejecución

| Modo | Comando | Descripción |
|------|---------|-------------|
| Tray | `CloverBridge.exe` | Sistema Tray (default) |
| Service | `CloverBridge.exe --service` | Windows Service |
| Console | `CloverBridge.exe --console` | Consola con logs |
| UI | `CloverBridge.exe --ui` | Dashboard WPF |

## Cambios desde versiones anteriores

### v1.0.0 (Actual)
- ✅ Implementación completa en C# .NET 8.0
- ✅ Multi-arquitectura (x86/x64)
- ✅ Single Instance Control
- ✅ UI optimizada para 1366x768
- ✅ WebSocket payment delivery
- ✅ System Tray integration
- ✅ Windows Service support
- ✅ Logging con Serilog
- ✅ Zero compilation errors

## Testing realizado

- ✅ Compilación sin errores
- ✅ Multi-instancia control verificado
- ✅ WebSocket payment delivery
- ✅ UI responsiva en baja resolución
- ✅ Logs en carpeta local
- ✅ Configuración persistente
- ✅ System Tray functionality
- ✅ x86 y x64 builds funcionales

## Descargas

- **CloverBridge-x64.exe**: Windows 10/11 64-bit (73.67 MB)
- **CloverBridge-x86.exe**: Windows 7 SP1+ 32-bit (67.15 MB)

## Licencia

MIT License

## Soporte

Para reportar bugs o solicitar features, abre un issue en GitHub.
