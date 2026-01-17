# 🚀 CloverBridge - Guía Completa de Compilación y Ejecución

**Fecha de actualización:** 16 de enero 2026  
**Estado:** ✅ COMPLETADO Y VERIFICADO

---

## 📋 Requisitos Previos

- **Windows 10/11 o Windows Server 2016+**
- **.NET 8.0 SDK** (descarga desde https://dotnet.microsoft.com/download)
- **PowerShell 5.1+** (incluido en Windows)

### Verificar Requisitos

```powershell
# Verificar versión de .NET
dotnet --version
# Debe mostrar: 8.0.x o superior

# Verificar PowerShell
$PSVersionTable.PSVersion
# Debe mostrar: 5.1 o superior
```

---

## ✅ Estado Actual del Proyecto

### Compilación
- ✅ **Debug**: Compila sin errores ni warnings
- ✅ **Release**: Compila sin errores ni warnings
- ✅ **Single-file executable**: Generado correctamente (~77 MB)

### Correcciones Aplicadas
1. ✅ Actualizado `System.Text.Json` de 8.0.4 a 8.0.5 (vulnerabilidad resuelta)
2. ✅ Null check agregado en `CloverWebSocketService.cs` línea 386
3. ✅ Reemplazado `Assembly.Location` con `AppContext.BaseDirectory` en 3 archivos:
   - `Program.cs`
   - `Models/AppConfig.cs`
   - `Services/ConfigurationService.cs`
4. ✅ Mejorado manejo de errores en `ApiService.cs` con reintentos automáticos

---

## 🔨 Compilación

### 1. Compilación en Modo Debug (Desarrollo)

```powershell
cd "d:\DEVs\Cloverwin"

# Limpiar y compilar
dotnet clean Cloverwin.sln
dotnet build Cloverwin.sln -c Debug

# Resultado: bin\Debug\net8.0-windows\CloverBridge.dll
```

**Uso:** Para debugging y desarrollo rápido.

### 2. Compilación en Modo Release (Producción)

```powershell
cd "d:\DEVs\Cloverwin"

# Compilar optimizado
dotnet build Cloverwin.sln -c Release

# Resultado: bin\Release\net8.0-windows\win-x64\CloverBridge.dll
```

**Uso:** Para producción con optimizaciones y mejor rendimiento.

### 3. Crear Ejecutable Single-File (Recomendado)

```powershell
cd "d:\DEVs\Cloverwin"

# Publicar como ejecutable standalone
dotnet publish Cloverwin.sln -c Release

# Resultado: bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe
# Tamaño: ~77 MB (auto-contenido, sin dependencias externas)
```

---

## ▶️ Ejecución

### Carpetas Necesarias

La aplicación crea automáticamente las siguientes carpetas en tiempo de ejecución:
```
<carpeta_app>/
├── INBOX/              # Archivo de entrada para transacciones
├── OUTBOX/             # Archivo de salida para respuestas
├── ARCHIVE/            # Archivos procesados
└── logs/               # Logs diarios de Serilog
```

### Modo 1: Aplicación de Tray (RECOMENDADO)

```powershell
cd "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish"

# Ejecutar sin argumentos (modo UI)
.\CloverBridge.exe
```

**Comportamiento:**
- Aparece en System Tray (esquina inferior derecha)
- Menú contextual: Connect, Disconnect, Dashboard, Exit
- Dashboard web en `http://localhost:3777`

### Modo 2: Modo Consola (Debugging)

```powershell
cd "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish"

# Ejecutar con salida de logs en consola
.\CloverBridge.exe --console
```

**Beneficios:**
- Ver todos los logs en tiempo real
- Presionar Ctrl+C para detener
- Útil para debugging y testing

### Modo 3: Windows Service (Producción)

```powershell
# Como administrador

cd "d:\DEVs\Cloverwin"

# 1. Compilar
.\build.ps1

# 2. Instalar como servicio
.\install-service.ps1

# 3. Iniciar servicio
Start-Service -Name "CloverBridge"

# 4. Ver estado
Get-Service -Name "CloverBridge"

# 5. Desinstalar (si necesario)
.\install-service.ps1 -Uninstall
```

---

## 📊 Verificación de Funcionamiento

### Test 1: Compilación Limpia
```powershell
cd "d:\DEVs\Cloverwin"
dotnet build Cloverwin.sln -c Release
# Debe mostrar: "Compilación correcta" sin errores
```

### Test 2: Ejecución en Consola
```powershell
cd "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish"
.\CloverBridge.exe --console

# Verificar logs:
# [INF] CloverBridge starting...
# [INF] Starting in console mode
# [INF] CloverWebSocketService starting
# [INF] TransactionQueueService started
# [INF] InboxWatcher started...
# [INF] API Server started on http://127.0.0.1:3777/
```

### Test 3: Verificar Configuración
```powershell
# Revisar archivo de configuración generado
cat "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish\config.json"
```

---

## 🔧 Scripts PowerShell Disponibles

### `build.ps1` - Compilar y Publicar
```powershell
.\build.ps1                          # Build Release por defecto
.\build.ps1 -Configuration Debug     # Build Debug
.\build.ps1 -Configuration Release   # Build Release explícito
```

### `start.ps1` - Quick Start (Desarrollo)
```powershell
.\start.ps1
# Limpia, compila y ejecuta en modo debug
```

### `verify.ps1` - Verificar Instalación
```powershell
.\verify.ps1
# Verifica .NET SDK, dependencias y estructura del proyecto
```

### `install-service.ps1` - Gestionar Windows Service
```powershell
.\install-service.ps1                # Instalar
.\install-service.ps1 -Uninstall     # Desinstalar
```

---

## 📝 Estructura del Proyecto

```
d:\DEVs\Cloverwin\
├── CloverBridge.csproj              # Configuración del proyecto
├── Cloverwin.sln                    # Solución
├── Program.cs                       # Punto de entrada
├── appsettings.json                 # Configuración por defecto
│
├── Models/                          # Modelos de datos
│   ├── AppConfig.cs                 # Configuración de app
│   └── CloverMessages.cs            # Protocolo Clover
│
├── Services/                        # Lógica backend
│   ├── ConfigurationService.cs      # Gestión de config
│   ├── CloverWebSocketService.cs    # Cliente WebSocket
│   ├── ApiService.cs                # API HTTP :3777
│   ├── TransactionQueueService.cs   # Cola FIFO
│   └── InboxWatcherService.cs       # Monitor de carpetas
│
├── UI/                              # Interfaz gráfica
│   ├── MainWindow.xaml(.cs)         # Ventana principal
│   ├── PairingWindow.xaml(.cs)      # Pairing visual
│   ├── ProductionMainWindow.xaml    # UI producción
│   └── TrayApplicationContext.cs    # System Tray
│
├── bin/
│   ├── Debug/net8.0-windows/        # Build debug
│   └── Release/net8.0-windows/
│       └── win-x64/
│           ├── CloverBridge.dll     # Assembly
│           └── publish/
│               └── CloverBridge.exe # ✅ EJECUTABLE
│
└── [scripts y documentación]
```

---

## ⚙️ Configuración

### Archivo de Configuración: `config.json`

Se crea automáticamente en la primera ejecución:

```json
{
  "clover": {
    "host": "10.1.1.53",        // IP de terminal Clover
    "port": 12345,              // Puerto WebSocket
    "merchantId": "default",
    "employeeId": "default"
  },
  "api": {
    "port": 3777,               // Puerto API HTTP
    "host": "127.0.0.1"
  },
  "folders": {
    "inbox": "INBOX",
    "outbox": "OUTBOX",
    "archive": "ARCHIVE"
  }
}
```

**Para editar:** Detener la aplicación, editar `config.json`, y reiniciar.

---

## 🐛 Troubleshooting

### Error: "Port 3777 is already in use"
```powershell
# El puerto ya está en uso por otra aplicación
# Opción 1: Cambiar puerto en config.json
# Opción 2: Liberar puerto
netstat -ano | findstr :3777
taskkill /PID <PID> /F
```

### Error: "Failed to connect to Clover"
- Verificar IP en `config.json` (por defecto: `10.1.1.53`)
- Verificar que la terminal Clover está encendida
- Verificar conectividad de red: `ping 10.1.1.53`

### Error: "Access Denied" en Windows Service
```powershell
# Ejecutar PowerShell como Administrador
# Y ejecutar:
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Logs
Todos los logs se guardan en:
```
<carpeta_app>/logs/clover-bridge-YYYY-MM-DD.log
```

---

## 📦 Dependencias (Incluidas en el Ejecutable)

- **Microsoft.Extensions.Hosting** v8.0.0
- **Microsoft.Extensions.Hosting.WindowsServices** v8.0.0
- **Microsoft.Extensions.Configuration.Json** v8.0.0
- **Serilog** v3.1.1
- **.NET Runtime** v8.0 (auto-contenido)

---

## ✨ Características Implementadas

- ✅ Cliente WebSocket a Clover POS
- ✅ API HTTP (puerto 3777)
- ✅ System Tray con menú contextual
- ✅ Ventana de Pairing (WPF)
- ✅ File Watcher (INBOX/OUTBOX)
- ✅ Cola de Transacciones FIFO
- ✅ Logs rotativos con Serilog
- ✅ Windows Service integrado
- ✅ Executable single-file portable
- ✅ Configuración JSON

---

## 🎯 Próximos Pasos

1. **Verificar conectividad Clover:**
   - Editar IP en `config.json`
   - Ejecutar con `--console` para ver logs en tiempo real

2. **Probar API:**
   ```powershell
   Invoke-WebRequest "http://localhost:3777/api/health"
   ```

3. **Instalar como servicio:**
   - Ejecutar `install-service.ps1` como Administrador
   - Configurar para inicio automático en Windows

4. **Monitoreo:**
   - Ver logs: `cat logs/clover-bridge-2026-01-16.log`
   - Verificar carpetas: INBOX, OUTBOX, ARCHIVE

---

## 📞 Soporte

Para problemas:
1. Revisar logs en `logs/` folder
2. Ejecutar con `--console` para debugging
3. Verificar `config.json` está bien configurado
4. Revisar conectividad de red con Clover

**Estado Actual:** ✅ 100% Compilable y Ejecutable
