# 🎉 CloverBridge v1.0.0 - ¡Lista para descargar!

## 📊 Estado del Proyecto

✅ **COMPLETADO Y PUBLICADO EN GITHUB**

- **Repositorio**: https://github.com/jdolan-exalink/cloverwin
- **Versión Actual**: v1.0.0
- **Fecha de Release**: 16 de Enero de 2026
- **Estado**: Producción
- **Compilación**: 0 errores, 0 warnings

## 📥 Descargar Ejecutables

### Versión para Windows 10/11 64-bit

```
Descargar: bin/Release/net8.0-windows/win-x64/publish/CloverBridge.exe
Tamaño: 73.67 MB
Arquitectura: x64 (64-bit)
Sistema Operativo: Windows 10, Windows 11
```

### Versión para Windows 7 SP1 32-bit

```
Descargar: bin/Release/net8.0-windows/win-x86/publish/CloverBridge.exe
Tamaño: 67.15 MB
Arquitectura: x86 (32-bit)
Sistema Operativo: Windows 7 SP1 y superior (32-bit)
```

## 🚀 Instalación Rápida

### 1. Descarga el ejecutable correcto para tu sistema

- **Windows 10/11 (64-bit)**: Descarga `CloverBridge-x64.exe`
- **Windows 7 SP1 (32-bit)**: Descarga `CloverBridge-x86.exe`

### 2. Copia el ejecutable a tu carpeta de destino

```powershell
# Ejemplo: Crear carpeta en C:\
New-Item -ItemType Directory -Path "C:\CloverBridge" -Force
Copy-Item -Path ".\CloverBridge.exe" -Destination "C:\CloverBridge\CloverBridge.exe"
```

### 3. Crea el archivo de configuración

Crea `appsettings.json` en la misma carpeta:

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
    "Inbox": "C:\\CloverBridge\\INBOX",
    "Outbox": "C:\\CloverBridge\\OUTBOX",
    "Archive": "C:\\CloverBridge\\ARCHIVE"
  },
  "Transaction": {
    "TimeoutMs": 120000,
    "Concurrency": 1
  }
}
```

### 4. Ejecuta la aplicación

```powershell
cd "C:\CloverBridge"
.\CloverBridge.exe
```

¡La aplicación aparecerá en el System Tray! 🎉

## 🔧 Modos de Ejecución

### Modo Tray (Default) - Sistema Tray

```powershell
.\CloverBridge.exe
```

- Se ejecuta en background
- Icono en system tray
- Menú contextual disponible
- Logs en carpeta `logs/`

### Modo Consola - Para Debug

```powershell
.\CloverBridge.exe --console
```

- Muestra logs en tiempo real
- Ideal para troubleshooting
- Presionar Ctrl+C para salir

### Modo Windows Service - Para Producción

```powershell
# Primero, copia el script install-service.ps1
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\install-service.ps1

# Verifica que esté instalado
Get-Service CloverBridge

# Inicia el servicio
Start-Service CloverBridge
```

### Modo UI - Dashboard Completo

```powershell
.\CloverBridge.exe --ui
```

- Interfaz WPF completa
- Panel de control visual
- Testing de transacciones

## ⚙️ Configuración

### Parámetros principales en appsettings.json

| Parámetro | Descripción | Valor por defecto |
|-----------|-------------|-------------------|
| `Clover.Host` | IP de la terminal Clover | `10.1.1.53` |
| `Clover.Port` | Puerto WebSocket Clover | `12345` |
| `Clover.Secure` | Usar WSS (HTTPS) | `false` |
| `Clover.RemoteAppId` | ID de aplicación remota | `clover-bridge` |
| `Clover.PosName` | Nombre del POS | `ERP Bridge` |
| `Clover.SerialNumber` | Número de serie | `CB-001` |
| `Api.Port` | Puerto HTTP local | `3777` |
| `Folders.Inbox` | Ruta carpeta INBOX | `./INBOX` |
| `Folders.Outbox` | Ruta carpeta OUTBOX | `./OUTBOX` |
| `Folders.Archive` | Ruta carpeta ARCHIVE | `./ARCHIVE` |
| `Transaction.TimeoutMs` | Timeout transacción (ms) | `120000` |

## 🔍 Verificar Instalación

### Comprueba que esté ejecutándose

```powershell
# Ver proceso
Get-Process CloverBridge

# Debe mostrar algo como:
# Handles  NPM(M)  PM(M)   WS(M)  CPU(s)   Id ProcessName
#    ---  ------  -----   -----  ------   -- -----------
#    150   42.5   150.3   250.1    0.85 8596 CloverBridge
```

### Verifica los logs

```powershell
# Ver últimas líneas del log
Get-Content .\logs\clover-bridge-*.log -Tail 20
```

### Test de conectividad

```powershell
# Ping a la terminal Clover
ping 10.1.1.53

# Test del puerto WebSocket
Test-NetConnection -ComputerName 10.1.1.53 -Port 12345

# Test del API HTTP local
Invoke-WebRequest -Uri "http://127.0.0.1:3777/health"
```

## 🆘 Troubleshooting

### Problema: "CloverBridge ya se está ejecutando"

**Solución**: Solo una instancia puede correr a la vez

```powershell
# Cierra la anterior desde el System Tray, o:
Get-Process CloverBridge | Stop-Process -Force

# Luego ejecuta de nuevo
.\CloverBridge.exe
```

### Problema: No conecta a terminal Clover

**Solución**: Verifica configuración

```powershell
# 1. Revisa appsettings.json
# - Host debe ser IP correcta
# - Port debe ser 12345 (u otro configurado)

# 2. Prueba conectividad
ping 10.1.1.53
Test-NetConnection -ComputerName 10.1.1.53 -Port 12345

# 3. Revisa logs
Get-Content .\logs\clover-bridge-*.log -Tail 50
```

### Problema: Puerto ocupado

**Solución**: Cambia puerto o detén proceso

```powershell
# Encuentra qué está usando el puerto
netstat -ano | findstr :3777

# O edita appsettings.json y cambia:
# "Api.Port": 3778  (en lugar de 3777)
```

## 📋 Requisitos del Sistema

### Mínimos

- **OS**: Windows 7 SP1 o superior
- **RAM**: 256 MB
- **Almacenamiento**: 100 MB
- **Red**: Acceso a terminal Clover
- **.NET**: 8.0 (incluido en ejecutable)

### Recomendados

- **OS**: Windows 10 o Windows 11
- **RAM**: 512 MB o superior
- **Almacenamiento**: SSD con 500 MB disponible
- **Red**: Conexión estable, latencia <100ms a Clover

## 📊 Información de Versión

```
CloverBridge v1.0.0
Fecha: 16 de Enero de 2026
Estado: Producción
Compilación: 0 errores, 0 warnings
Framework: .NET 8.0
Lenguaje: C# 12
Arquitectura: x86 + x64
Tamaño: 67-73 MB (según arquitectura)
```

## 📚 Documentación

- **[README.md](README.md)** - Descripción general
- **[INSTALLATION.md](INSTALLATION.md)** - Guía de instalación detallada
- **[CHANGELOG.md](CHANGELOG.md)** - Historial de cambios

## 🔗 Enlaces

- **GitHub**: https://github.com/jdolan-exalink/cloverwin
- **Issues**: https://github.com/jdolan-exalink/cloverwin/issues
- **Releases**: https://github.com/jdolan-exalink/cloverwin/releases

## 🤝 Soporte

Si encuentras problemas:

1. Revisa los logs en `logs/`
2. Comprueba la configuración en `appsettings.json`
3. Abre un issue en GitHub con:
   - Sistema operativo
   - Versión de CloverBridge
   - Logs relevantes
   - Descripción del problema

## 📝 Licencia

MIT License - Libre para uso comercial y personal

---

**¡Gracias por usar CloverBridge!** 🚀

Para mantenerlo actualizado, vigila la [página de releases](https://github.com/jdolan-exalink/cloverwin/releases).
