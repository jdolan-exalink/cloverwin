# Mejoras Implementadas - UI C# CloverBridge

## ✅ Cambios Completados

### 1. **Relocación de Carpetas al Directorio del Ejecutable**

Se modificaron los archivos para que todas las carpetas de datos (INBOX, OUTBOX, ARCHIVE, logs) y el archivo de configuración se creen en el mismo directorio donde está el ejecutable `.exe`, en lugar de usar `%APPDATA%`.

**Archivos modificados:**

- `windows/Services/ConfigurationService.cs` - Ahora usa `Assembly.GetExecutingAssembly().Location`
- `windows/Models/AppConfig.cs` - Método `GetExecutableDirectory()` para rutas relativas
- `windows/Program.cs` - Logging ahora usa carpeta local `./logs`
- `windows/UI/MainWindow.xaml.cs` - Todas las operaciones de guardado usan configuración centralizada

**Estructura creada:**

```
CloverBridge.exe
├── INBOX/          ← Solicitudes de transacciones
├── OUTBOX/         ← Respuestas del terminal
├── ARCHIVE/        ← Transacciones procesadas
├── logs/           ← Logs de la aplicación
└── config.json     ← Configuración
```

### 2. **Corrección de Test de Pago**

Se actualizaron todos los métodos de testing para usar las rutas correctas de la configuración:

**Métodos actualizados en `MainWindow.xaml.cs`:**

- `GenerateQRButton_Click` - Genera solicitudes QR en INBOX
- `SendSaleButton_Click` - Crea transacciones de venta en INBOX
- `SendAuthButton_Click` - Crea autorizaciones en INBOX

**Cambios implementados:**

```csharp
// Antes (hardcoded):
var inboxPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "CloverBridge",
    "inbox"
);

// Ahora (desde configuración):
var config = _configService.GetConfig();
var inboxPath = config.Folders.Inbox;
```

### 3. **Mejoras en Logging**

Se agregó información adicional en los logs para debugging:

- ✅ Muestra la ruta completa donde se guardan los archivos
- ✅ Nombres de archivo con timestamp único
- ✅ Confirmación visual en UI

### 4. **UI Moderna**

La UI actual incluye:

- ✅ Diseño oscuro profesional
- ✅ Tabs organizados (QR, Venta, Auth, Void, Refund, Config, Logs)
- ✅ Status badges con colores (Conectado/Desconectado)
- ✅ Editor de configuración integrado
- ✅ Logs en tiempo real con scroll automático
- ✅ Botones con hover effects

## 📋 Cómo Usar

### Iniciar la Aplicación

```powershell
# Opción 1: Con UI (Testing Dashboard)
.\CloverBridge.exe --ui

# Opción 2: Como consola
.\CloverBridge.exe --console

# Opción 3: Como servicio
.\CloverBridge.exe --service

# Opción 4: System Tray (por defecto)
.\CloverBridge.exe
```

### Probar Transacciones

1. **Configurar Terminal:**

   - Ir a la pestaña "Config"
   - Ingresar IP del terminal Clover (ej: `10.1.1.53`)
   - Guardar configuración

2. **Generar QR de Pago:**

   - Ir a pestaña "QR Code"
   - Ingresar monto (ej: `1000` = $10.00)
   - Click "Generar QR"
   - Se crea archivo en `INBOX/qr_TIMESTAMP.json`

3. **Enviar Venta:**
   - Ir a pestaña "Venta"
   - Ingresar monto
   - Click "Enviar Venta"
   - Se crea archivo en `INBOX/sale_TIMESTAMP.json`

### Ver Logs

- **En la UI:** Pestaña "Logs" muestra eventos en tiempo real
- **En archivos:** Carpeta `logs/` con archivos diarios

## 🔧 Compilar

```powershell
cd windows
dotnet build CloverBridge.csproj --configuration Debug
```

## 🚀 Próximas Mejoras Sugeridas

### UI Web-Style (Pendiente)

Para igualar completamente a la UI web, se podrían agregar:

- [ ] Gradiente de fondo (#667eea a #764ba2)
- [ ] Animaciones de pulso en status badges
- [ ] Tabs con diseño más moderno
- [ ] Iconos vectoriales
- [ ] Sombras y efectos de profundidad

### System Tray Completo (Parcial)

Actualmente `TrayApplicationContext.cs` tiene menú básico. Mejorar:

- [ ] Mostrar/ocultar ventana principal
- [ ] Quick actions desde el menú
- [ ] Notificaciones de transacciones
- [ ] Status en el tooltip del icono

### Funcionalidades Adicionales

- [ ] Historial de transacciones
- [ ] Búsqueda de transacciones
- [ ] Exportar logs a CSV
- [ ] Modo oscuro/claro toggle
- [ ] Configuración de temas

## 📝 Notas Técnicas

### Por qué usar Directorio del Ejecutable

- ✅ Aplicación portable (copiar carpeta completa)
- ✅ No requiere permisos de AppData
- ✅ Fácil backup de configuración
- ✅ Desarrollo más simple (ver archivos creados)

### Manejo de SSL/TLS

El servicio incluye bypass de certificados auto-firmados:

```csharp
RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true
```

**Nota:** Solo para desarrollo. En producción validar certificados correctamente.

### Threading

La UI usa `STA` (Single-Threaded Apartment) requerido por WPF:

```csharp
var thread = new Thread(() => {
    var app = new Application();
    app.Run(new MainWindow(...));
});
thread.SetApartmentState(ApartmentState.STA);
```

## 🐛 Troubleshooting

### La UI no conecta al terminal

1. Verificar IP del terminal en pestaña "Config"
2. Asegurar que el puerto sea correcto (default: 12345)
3. Verificar que `secure: true` esté correcto
4. Revisar logs en carpeta `logs/`

### No se crean archivos en INBOX

1. Verificar permisos de escritura en carpeta
2. Ver logs para mensajes de error
3. Confirmar que `config.json` tenga rutas correctas

### Error al compilar

```powershell
# Si el .exe está corriendo, detenerlo:
Get-Process CloverBridge | Stop-Process -Force

# Luego recompilar:
dotnet build
```

## 📊 Estado del Proyecto

| Componente          | Estado       | Notas                          |
| ------------------- | ------------ | ------------------------------ |
| UI WPF              | ✅ Completo  | 474 líneas, 6 tabs funcionales |
| Relocación de rutas | ✅ Completo  | Todo en directorio ejecutable  |
| Test de pago        | ✅ Corregido | Usa configuración centralizada |
| System Tray         | 🟡 Parcial   | Menú básico implementado       |
| Estilo web          | 🟡 Parcial   | Funcional pero no idéntico     |
| Compilación         | ✅ OK        | 0 errores, 1 warning (JSON)    |

---

**Última actualización:** 15/01/2026
**Versión:** 1.0.0-alpha
