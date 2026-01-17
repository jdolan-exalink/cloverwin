# CloverBridge - Windows Application

Aplicación Windows completa con UI de testing integrada para CloverBridge.

## 🎯 Características

- ✅ **UI de Testing Completa**: Dashboard WPF moderno con todas las funciones de testing
- ✅ **Modo Bandeja del Sistema**: Ejecución en background con acceso rápido
- ✅ **Generación de QR**: Interfaz visual para crear códigos QR
- ✅ **Testing de Transacciones**: Ventas, autorizaciones, anulaciones y devoluciones
- ✅ **Logs en Tiempo Real**: Visualización de respuestas y estado del sistema
- ✅ **Configuración Visual**: Editor integrado de configuración
- ✅ **Pairing Visual**: Ventana dedicada para el proceso de pairing
- ✅ **Windows Service**: Ejecución como servicio del sistema
- ✅ **API HTTP**: Puerto 3777 para integración externa

## 🚀 Modos de Ejecución

### 1. Testing Dashboard UI (Recomendado para desarrollo)

```powershell
.\launch-ui.ps1
# O directamente:
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

**Características:**

- Ventana completa con UI de testing
- Generación de QR visual
- Testing de transacciones
- Logs en tiempo real
- Configuración integrada

### 2. Modo Bandeja del Sistema (Default)

```powershell
.\launch-tray.ps1
# O directamente:
.\bin\Debug\net8.0-windows\CloverBridge.exe
```

**Características:**

- Ejecuta en background
- Ícono en bandeja del sistema
- Doble clic para abrir Testing UI
- Menú contextual con opciones
- Mínimo uso de recursos

### 3. Modo Consola (Para debugging)

```powershell
.\bin\Debug\net8.0-windows\CloverBridge.exe --console
```

### 4. Windows Service

```powershell
.\bin\Debug\net8.0-windows\CloverBridge.exe --service
```

## 📦 Compilación

### Compilación Debug (Desarrollo)

```powershell
dotnet build CloverBridge.csproj --configuration Debug
```

### Compilación Release (Producción)

```powershell
.\build-release.ps1

# O manualmente:
dotnet publish CloverBridge.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true
```

El ejecutable se generará en:

```
.\bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe
```

## 🎨 Testing Dashboard UI

### Funcionalidades

#### 1. Generación de QR

- Campo de monto configurable
- Generación visual de QR code
- Visualización del código generado
- Guardado automático en inbox

#### 2. Testing de Ventas

- Monto configurable
- External ID automático o manual
- Envío directo a inbox

#### 3. Testing de Autorizaciones

- Pre-autorización de montos
- Seguimiento de transacciones

#### 4. Anulaciones y Devoluciones

- Por Payment ID
- Confirmación visual
- Log detallado

#### 5. Logs y Monitoreo

- **Tab Respuestas**: Respuestas del terminal Clover
- **Tab Sistema**: Logs de la aplicación
- **Tab Configuración**: Editor de configuración

### Indicadores Visuales

- 🟢 **Verde**: Conectado/Pareado
- 🟡 **Amarillo**: Conectando/Pairing requerido
- 🔴 **Rojo**: Error/Desconectado

## ⚙️ Configuración

### Desde la UI

1. Abrir Testing Dashboard (`.\launch-ui.ps1`)
2. Ir al tab "Configuración"
3. Editar valores:
   - Merchant ID
   - Device ID
   - Token
   - Secure (wss:// o ws://)
4. Guardar configuración
5. Reiniciar aplicación

### Archivo de Configuración

Ubicación: `%APPDATA%\CloverBridge\config.json`

```json
{
  "clover": {
    "merchantId": "YOUR_MERCHANT_ID",
    "deviceId": "YOUR_DEVICE_ID",
    "token": "YOUR_TOKEN",
    "secure": false
  },
  "api": {
    "host": "localhost",
    "port": 3777
  }
}
```

## 📁 Estructura de Carpetas

```
%APPDATA%\CloverBridge\
├── config.json          # Configuración
├── inbox/              # Solicitudes entrantes
├── outbox/             # Respuestas salientes
└── logs/               # Archivos de log
```

## 🔧 Desarrollo

### Requisitos

- .NET 8.0 SDK
- Windows 10/11
- Visual Studio 2022 (opcional)

### Scripts Disponibles

| Script              | Descripción                 |
| ------------------- | --------------------------- |
| `launch-ui.ps1`     | Inicia Testing Dashboard UI |
| `launch-tray.ps1`   | Inicia en modo bandeja      |
| `build-release.ps1` | Compila versión Release     |
| `start.ps1`         | Script genérico de inicio   |
| `verify.ps1`        | Verifica instalación        |

### Arquitectura

```
CloverBridge/
├── Program.cs              # Entry point
├── Models/                 # Modelos de datos
├── Services/              # Servicios backend
│   ├── CloverWebSocketService.cs
│   ├── ConfigurationService.cs
│   ├── TransactionQueueService.cs
│   ├── InboxWatcherService.cs
│   └── ApiService.cs
└── UI/                    # Interfaces de usuario
    ├── MainWindow.xaml        # Testing Dashboard
    ├── PairingWindow.xaml     # Ventana de pairing
    └── TrayApplicationContext.cs  # Bandeja del sistema
```

## 🧪 Testing

### Flujo de Testing Típico

1. **Iniciar aplicación**

   ```powershell
   .\launch-ui.ps1
   ```

2. **Verificar conexión**

   - Estado debe mostrar "Conectado" o "Pareado"
   - Si muestra "Pairing Requerido", hacer clic en botón "Pairing"

3. **Generar QR de prueba**

   - Ingresar monto (ej: 1000)
   - Clic en "Generar QR Code"
   - Verificar en tab "Respuestas"

4. **Realizar venta**

   - Ingresar monto (ej: 2500)
   - Clic en "Enviar Venta"
   - Confirmar en terminal Clover
   - Ver respuesta en logs

5. **Anular/Devolver**
   - Copiar Payment ID de la respuesta
   - Pegarlo en campo "Payment ID"
   - Clic en "Anular" o "Devolver"

## 📊 Monitoreo

### Desde la UI

- Tab "Sistema": Eventos de la aplicación
- Tab "Respuestas": Respuestas del terminal

### Logs en Archivo

```powershell
# Abrir carpeta de logs
cd $env:APPDATA\CloverBridge\logs

# Ver último log
Get-Content .\clover-bridge-$(Get-Date -Format 'yyyyMMdd').log -Tail 50
```

## 🐛 Troubleshooting

### La UI no abre

```powershell
# Verificar proceso
Get-Process CloverBridge

# Detener y reiniciar
Get-Process CloverBridge | Stop-Process -Force
.\launch-ui.ps1
```

### No se conecta al terminal

1. Verificar configuración en tab "Configuración"
2. Verificar red (terminal y PC en misma red)
3. Revisar logs del sistema

### Inbox no procesa archivos

```powershell
# Verificar servicio de watcheo
# Ver logs en tab Sistema
# Reiniciar aplicación
```

## 📚 Recursos Adicionales

- [Clover API Documentation](https://docs.clover.com/)
- [Network Pay Display](https://docs.clover.com/docs/network-pay-display)
- [Pairing Process](https://docs.clover.com/docs/pairing-with-clover-devices)

## 🔐 Seguridad

- Credentials almacenadas localmente en `%APPDATA%`
- Conexión WebSocket configurable (ws:// o wss://)
- Logs no contienen información sensible

## 📝 Changelog

### Version 1.0.0

- ✅ UI de Testing completa con WPF
- ✅ Modo bandeja del sistema
- ✅ Testing de QR, ventas, auth, void, refund
- ✅ Logs en tiempo real
- ✅ Configuración visual
- ✅ Pairing visual
- ✅ Scripts de lanzamiento
- ✅ Compilación Release optimizada

## 📄 Licencia

Propiedad de Clover Bridge Team - 2026

---

**¿Necesitas ayuda?** Revisa los logs o contacta al equipo de desarrollo.
