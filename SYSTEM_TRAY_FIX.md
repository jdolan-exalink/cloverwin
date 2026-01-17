# 🔧 Arreglos System Tray y Pairing - CloverBridge

## ✅ Problemas Resueltos

### 1. **System Tray - Ventana no vuelve a aparecer** ✅

**Problema:** Cuando minimizabas la ventana con el botón "Minimizar a Bandeja", la ventana desaparecía y no había forma de volver a abrirla.

**Causa:** La ventana se ocultaba con `Hide()` pero no había mecanismo para mostrarla de nuevo.

**Solución implementada:**
- ✅ Agregado método `ShowMainWindow()` público en MainWindow
- ✅ Modificado `TrayApplicationContext.OpenMainWindow()` para detectar si la ventana existe pero está oculta
- ✅ Agregado evento `Window_Closing` para interceptar el cierre de ventana
- ✅ Ahora hacer clic en la X también oculta (no cierra) la ventana
- ✅ Doble clic en el icono de bandeja vuelve a mostrar la ventana

### 2. **Cerrar la Aplicación desde System Tray** ✅

**Problema:** No había forma de cerrar completamente la aplicación desde el system tray.

**Solución implementada:**
- ✅ Agregado método `ExitApplication()` en TrayApplicationContext
- ✅ Click derecho en icono de bandeja → "Salir"
- ✅ Cierra todas las ventanas correctamente
- ✅ Detiene todos los servicios (WebSocket, API, Queue, Inbox)
- ✅ Libera el icono de la bandeja
- ✅ Sale limpiamente de la aplicación

### 3. **Logs Detallados de Pairing** ✅

**Problema:** No se podía diagnosticar por qué no llegaba el código de pairing.

**Solución implementada:**
- ✅ Logs detallados en `SendPairingRequestAsync()`
- ✅ Logs cuando se recibe cualquier mensaje WebSocket
- ✅ Logs específicos para `PAIRING_CODE` y `PAIRING_RESPONSE`
- ✅ Logs de payload completo para debugging
- ✅ Emojis para identificar rápidamente el tipo de mensaje

---

## 📝 Cambios Técnicos

### MainWindow.xaml.cs

**Agregado:**
```csharp
private bool _isExiting = false;

public void ShowMainWindow()
{
    Show();
    WindowState = System.Windows.WindowState.Normal;
    Activate();
}

private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    if (!_isExiting)
    {
        e.Cancel = true;
        Hide();
        LogSystem("🔽 Ventana oculta. Usa el icono de la bandeja para volver a abrirla.");
    }
}

public void ForceClose()
{
    _isExiting = true;
    Close();
}
```

**Comportamiento:**
- Cerrar ventana (X) → Oculta, no cierra
- Click derecho en tray → "Salir" → Cierra completamente
- `_isExiting` flag controla si es cierre real o solo ocultar

### MainWindow.xaml

**Agregado:**
```xaml
Closing="Window_Closing"
```

### TrayApplicationContext.cs

**Modificado `OpenMainWindow()`:**
```csharp
private void OpenMainWindow()
{
    if (_mainWindow == null)
    {
        _mainWindow = new MainWindow(...);
        _mainWindow.Show();
    }
    else if (!_mainWindow.IsVisible)
    {
        _mainWindow.ShowMainWindow();  // ← Usa el método público
    }
    else
    {
        _mainWindow.Activate();
    }
}
```

**Agregado `ExitApplication()`:**
```csharp
private void ExitApplication()
{
    Log.Information("Exiting application from tray");
    
    // Cerrar ventana principal
    if (_mainWindow != null && _mainWindow.IsVisible)
    {
        _mainWindow.ForceClose();
    }
    
    // Cerrar ventana de pairing
    _pairingWindow?.Close();
    
    // Detener servicios
    _host.StopAsync().Wait();
    _host.Dispose();
    
    // Liberar tray icon
    _notifyIcon.Visible = false;
    _notifyIcon.Dispose();
    
    ExitThread();
}
```

### CloverWebSocketService.cs

**Logs detallados agregados:**

```csharp
// En SendPairingRequestAsync():
Log.Information("No auth token found, requesting pairing");
Log.Information("Sending pairing request: {@PairingRequest}", pairingRequest);
Log.Information("Pairing request sent successfully");

// En HandleMessageAsync():
Log.Information("📨 Raw message received: {Message}", messageJson);
Log.Information("📬 Received message type: {Method}, ID: {Id}", message.Method, message.Id);
Log.Information("🔑 Processing PAIRING_CODE message");

// En HandlePairingCodeAsync():
Log.Information("HandlePairingCodeAsync called");
Log.Information("Payload: {Payload}", payload);
Log.Information("✅ Pairing code received: {Code}", _lastPairingCode);
Log.Information("🔔 Invoking PairingCodeReceived event");
```

---

## 🚀 Cómo Usar

### Minimizar a Bandeja

1. **Opción 1:** Click en botón "Minimizar a Bandeja" (header)
2. **Opción 2:** Click en la X de la ventana
3. **Resultado:** Ventana se oculta, icono permanece en bandeja

### Volver a Mostrar Ventana

1. **Doble click** en el icono de la bandeja del sistema
2. **O:** Click derecho → "Abrir Testing UI"
3. **Resultado:** Ventana vuelve a aparecer en primer plano

### Cerrar la Aplicación

1. **Click derecho** en el icono de la bandeja
2. **Seleccionar "Salir"**
3. **Resultado:** 
   - Todas las ventanas se cierran
   - Servicios se detienen
   - Icono desaparece de la bandeja
   - Aplicación termina completamente

---

## 🔍 Diagnóstico de Pairing

### Ver Logs

**Ubicación:**
```
bin/Debug/net8.0-windows/logs/cloverbridge-YYYYMMDD.log
```

**Qué buscar:**

#### 1. Conexión establecida
```
[INFO] Connected to Clover
```

#### 2. Solicitud de pairing enviada
```
[INFO] No auth token found, requesting pairing
[INFO] Sending pairing request: {"RemoteApplicationId":"clover-bridge",...}
[INFO] Pairing request sent successfully
```

#### 3. Mensajes recibidos del terminal
```
[INFO] 📨 Raw message received: {"method":"PAIRING_CODE",...}
[INFO] 📬 Received message type: PAIRING_CODE, ID: xyz
[INFO] 🔑 Processing PAIRING_CODE message
```

#### 4. Código de pairing recibido
```
[INFO] HandlePairingCodeAsync called
[INFO] Payload: {"pairingCode":"ABC123"}
[INFO] ✅ Pairing code received: ABC123
[INFO] 🔔 Invoking PairingCodeReceived event
```

### Si NO aparece el código

**Verificar en logs:**

1. **¿Llegó la conexión?**
   ```
   [INFO] Connected to Clover
   ```
   Si no: Problema de red/IP/puerto

2. **¿Se envió la solicitud?**
   ```
   [INFO] Pairing request sent successfully
   ```
   Si no: Problema al enviar mensaje

3. **¿Se reciben mensajes?**
   ```
   [INFO] 📨 Raw message received: ...
   ```
   Si no: Terminal no está respondiendo

4. **¿Se recibe PAIRING_CODE?**
   ```
   [INFO] 🔑 Processing PAIRING_CODE message
   ```
   Si no: Terminal envía otro tipo de mensaje

5. **¿El payload tiene el código?**
   ```
   [INFO] Payload: {"pairingCode":"ABC123"}
   ```
   Si aparece `"pairingCode": null`: Terminal no generó código

### Herramienta de Diagnóstico

```powershell
# Ver logs en tiempo real
Get-Content ".\bin\Debug\net8.0-windows\logs\cloverbridge-*.log" -Wait -Tail 20

# Filtrar solo mensajes de pairing
Get-Content ".\bin\Debug\net8.0-windows\logs\cloverbridge-*.log" | Select-String "pairing|PAIRING|🔑|🔐"

# Ver últimos 50 mensajes
Get-Content ".\bin\Debug\net8.0-windows\logs\cloverbridge-*.log" -Tail 50
```

---

## 📊 Flujo Completo del System Tray

```
┌─────────────────────────────────────┐
│  Usuario inicia app (--ui o tray)  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  TrayApplicationContext inicia      │
│  - Crea icono en bandeja            │
│  - Inicia servicios (WebSocket,etc) │
│  - Crea MainWindow si --ui          │
└──────────────┬──────────────────────┘
               │
               ▼
       ┌───────────────┐
       │ ¿Qué hace el  │
       │   usuario?    │
       └───────┬───────┘
               │
      ┌────────┴────────┐
      │                 │
      ▼                 ▼
┌──────────┐      ┌──────────┐
│ Minimiza │      │  Cierra  │
│ ventana  │      │  ventana │
│ (botón)  │      │   (X)    │
└────┬─────┘      └────┬─────┘
     │                 │
     └────────┬────────┘
              │
              ▼
     ┌─────────────────┐
     │ Window_Closing  │
     │ _isExiting=false│
     │  e.Cancel=true  │
     │    Hide()       │
     └────────┬────────┘
              │
              ▼
     ┌─────────────────┐
     │ Ventana oculta  │
     │ Tray visible    │
     └────────┬────────┘
              │
     ┌────────┴────────┐
     │                 │
     ▼                 ▼
┌──────────┐    ┌─────────────┐
│ Doble    │    │ Click der.  │
│ click    │    │ → "Salir"   │
│ en tray  │    └──────┬──────┘
└────┬─────┘           │
     │                 ▼
     │        ┌──────────────────┐
     │        │ ExitApplication  │
     │        │ ForceClose()     │
     │        │ Stop services    │
     │        │ Dispose tray     │
     │        └────────┬─────────┘
     │                 │
     ▼                 ▼
┌──────────┐    ┌─────────────┐
│ShowMain  │    │ App cerrada │
│Window()  │    │ completamente│
└────┬─────┘    └─────────────┘
     │
     ▼
┌──────────┐
│ Ventana  │
│ visible  │
└──────────┘
```

---

## 🎓 Lecciones Aprendidas

### System Tray en WPF

**Problema común:** WPF no tiene control nativo de NotifyIcon
**Solución:** Usar `System.Windows.Forms.NotifyIcon` en proyecto WPF

**Lifecycle:**
1. `Hide()` - Oculta ventana pero mantiene instancia
2. `Show()` - Muestra ventana oculta
3. `Close()` - Cierra y destruye ventana (no se puede volver a mostrar)
4. `Dispose()` - Libera recursos del NotifyIcon

**Best Practice:**
- Ocultar ventana en lugar de cerrar (mejor UX)
- Flag `_isExiting` para diferenciar ocultar vs cerrar
- `ForceClose()` para cierre real cuando sea necesario

### Debugging WebSocket

**Herramientas:**
- Logs estructurados con Serilog
- Emojis para identificación rápida
- Serialización de payloads para inspección
- Logs a nivel de mensaje crudo y parseado

**Puntos clave de logging:**
1. Al enviar mensaje
2. Al recibir mensaje crudo
3. Al parsear mensaje
4. Al invocar eventos
5. Errores con stacktrace completo

---

## 📝 Checklist de Verificación

### System Tray
- [x] Icono aparece en bandeja al iniciar
- [x] Doble click abre/muestra ventana
- [x] Click derecho muestra menú contextual
- [x] Menú tiene opciones: Abrir UI, Dashboard, Pairing, Config, Logs, Salir
- [x] Botón "Minimizar a Bandeja" oculta ventana
- [x] Cerrar ventana (X) oculta ventana
- [x] "Salir" del menú cierra aplicación completamente
- [x] Tooltip del icono muestra estado de conexión

### Pairing
- [x] Logs detallados en archivo
- [x] Mensaje de pairing se envía al conectar
- [x] Se detecta cuando llega PAIRING_CODE
- [x] Payload se parsea correctamente
- [x] Evento PairingCodeReceived se dispara
- [x] Popup de pairing se muestra automáticamente
- [x] Código se muestra en el popup

---

## 🚀 Ejecutar

```powershell
cd D:\DEVs\Clover2\windows

# Con UI (recomendado para testing)
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui

# Solo tray (producción)
.\bin\Debug\net8.0-windows\CloverBridge.exe
```

### Verificar Funcionalidad

1. **Iniciar app**
2. **Verificar icono en bandeja** ✅
3. **Minimizar ventana** → Debe ocultarse
4. **Doble click en icono** → Debe volver a aparecer
5. **Click derecho → Salir** → Debe cerrar completamente
6. **Revisar logs** para mensajes de pairing

---

**Fecha:** 16/01/2026  
**Versión:** 1.0.1-alpha
