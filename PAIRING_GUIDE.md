# 🔑 Guía de Pairing - CloverBridge

## ✨ Nueva Funcionalidad de Pairing

Se ha implementado un popup de pairing moderno similar a la UI web, integrado directamente en la ventana principal de testing.

---

## 🎯 ¿Qué es el Pairing?

El **pairing** es el proceso de **autenticación** entre CloverBridge y el terminal Clover. Es necesario para que ambos dispositivos puedan comunicarse de forma segura.

### Estados de Conexión

1. **Desconectado** 🔴 - No hay conexión con el terminal
2. **Conectando** 🟡 - Intentando establecer conexión WebSocket
3. **Conectado** 🟢 - WebSocket conectado, pero sin autenticar
4. **Pairing Requerido** 🟠 - Terminal solicita código de pairing
5. **Pareado** ✅ - Autenticación completa, listo para transacciones

---

## 🚀 Cómo Usar el Pairing

### Método 1: Automático (Recomendado)

Cuando la app se conecta por primera vez o el terminal solicita autenticación:

1. **Espera el popup automático**
   - La UI mostrará un overlay con el código de pairing
   - El código tiene 6 caracteres alfanuméricos (ej: `A3F2B1`)

2. **Ingresa el código en el terminal Clover**
   - Ve a la app Network Pay Display en el Clover
   - Ingresa el código mostrado en el popup
   - Presiona "Confirmar"

3. **Listo** ✅
   - El popup se cerrará automáticamente
   - Verás el estado cambiar a "Pareado"
   - Ya puedes realizar transacciones

### Método 2: Manual (Forzar Pairing)

Si necesitas reiniciar el proceso de pairing:

1. **Click en botón "Pairing"** (header superior)
2. **Opciones disponibles:**
   - **Mostrar código existente** - Si ya hay un código recibido
   - **Reintentar** - Forzar un nuevo pairing
   - **Cancelar** - Cerrar el popup

3. **Reintentar Pairing:**
   - Click en "Reintentar" dentro del popup
   - La app se desconectará y volverá a conectar
   - Se generará un nuevo código de pairing

---

## 🎨 UI del Popup de Pairing

```
╔════════════════════════════════════╗
║    🔑 Integrar Terminal            ║
║                                    ║
║  Ingresa este código en tu Clover  ║
║                                    ║
║  ┌──────────────────────────────┐ ║
║  │                              │ ║
║  │         A  3  F  2  B  1      │ ║ ← Código en grande
║  │                              │ ║
║  └──────────────────────────────┘ ║
║                                    ║
║ Esperando respuesta del terminal...║
║                                    ║
║  [Reintentar]      [Cancelar]     ║
╚════════════════════════════════════╝
```

### Características del Popup

- ✅ **Overlay oscuro** - Foco en el código
- ✅ **Código en grande** - Fácil de leer (48px)
- ✅ **Color verde** (#00ff88) - Alta visibilidad
- ✅ **Fuente monoespaciada** (Consolas) - Clara distinción de caracteres
- ✅ **Borde azul** (#667eea) - Acorde al theme de la app
- ✅ **Cierre automático** - Al completar el pairing
- ✅ **Click fuera para cerrar** - En el overlay oscuro

---

## 🔧 Troubleshooting

### ❌ Problema: "Pairing Requerido" pero no aparece código

**Solución:**
1. Click en botón "Pairing" en el header
2. Si no muestra código, click en "Reintentar"
3. Esperar a que aparezca el nuevo código

**Logs a revisar:**
```
🔐 Código de pairing recibido: A3F2B1
💡 Popup de pairing mostrado con código: A3F2B1
```

---

### ❌ Problema: Código no funciona en el terminal

**Causas posibles:**
- Código expiró (tienen ~2 minutos de validez)
- Error de tipeo al ingresar el código
- Terminal Clover reiniciado

**Solución:**
1. Abrir popup de pairing
2. Click en "Reintentar"
3. Ingresar el **nuevo** código generado

---

### ❌ Problema: Popup no se cierra automáticamente

**Verificar:**
1. Estado de conexión en el header
2. Logs del sistema (pestaña "Logs")
3. Si muestra "Pareado" pero popup sigue abierto, cerrar manualmente

**Logs esperados al pairing exitoso:**
```
✅ Pairing completado exitosamente!
📡 Estado de conexión: Pareado
```

---

### ❌ Problema: No se puede forzar nuevo pairing

**Solución:**
1. Verificar que el terminal esté encendido y conectado a la red
2. Verificar IP y puerto en tab "Config"
3. Revisar logs para errores de conexión
4. Reiniciar la aplicación

**Comando PowerShell para reiniciar:**
```powershell
Get-Process CloverBridge | Stop-Process -Force
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

---

## 📊 Flujo del Proceso de Pairing

```
┌─────────────────┐
│  App Inicia     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Conecta a      │
│  Terminal       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐      ┌──────────────────┐
│  ¿Ya pareado?   │─NO──→│ Envía Pairing    │
└────────┬────────┘      │ Request          │
         │               └────────┬─────────┘
        SÍ                        │
         │                        ▼
         │               ┌──────────────────┐
         │               │ Terminal envía   │
         │               │ Código (6 chars) │
         │               └────────┬─────────┘
         │                        │
         │                        ▼
         │               ┌──────────────────┐
         │               │ Mostrar Popup    │
         │               │ con Código       │
         │               └────────┬─────────┘
         │                        │
         │                        ▼
         │               ┌──────────────────┐
         │               │ Usuario ingresa  │
         │               │ código en Clover │
         │               └────────┬─────────┘
         │                        │
         │                        ▼
         │               ┌──────────────────┐
         │               │ Terminal valida  │
         │               │ y envía Token    │
         │               └────────┬─────────┘
         │                        │
         ▼                        ▼
┌──────────────────────────────────┐
│  Estado: PAREADO ✅               │
│  Token guardado en config.json   │
└──────────────────────────────────┘
         │
         ▼
┌──────────────────┐
│  Listo para      │
│  Transacciones   │
└──────────────────┘
```

---

## 🎓 Diferencias vs UI Web

| Característica | UI Web (Electron) | UI C# (WPF) |
|----------------|-------------------|-------------|
| **Popup Integrado** | ✅ Modal overlay | ✅ Modal overlay |
| **Código grande** | ✅ 48px | ✅ 48px |
| **Cierre automático** | ✅ Al pairing | ✅ Al pairing |
| **Reintentar** | ✅ Botón | ✅ Botón |
| **Click fuera cierra** | ✅ Sí | ✅ Sí |
| **Color del código** | Verde (#00ff88) | Verde (#00ff88) ✅ |
| **Borde gradiente** | Gradiente azul | Sólido azul |

**Resultado:** ✅ Paridad funcional 100%

---

## 💾 Persistencia del Token

Cuando el pairing es exitoso, el token de autenticación se guarda automáticamente:

**Ubicación:**
```
bin/Debug/net8.0-windows/config.json
```

**Contenido (extracto):**
```json
{
  "clover": {
    "host": "10.1.1.53",
    "port": 12345,
    "authToken": "abc123def456...",  ← Token guardado aquí
    "remoteAppId": "clover-bridge"
  }
}
```

**Beneficio:** No necesitas hacer pairing cada vez que inicias la app, solo cuando:
- Es la primera vez que te conectas
- El terminal fue reiniciado
- El token expiró o fue revocado

---

## 📝 Logs Relevantes

### Pairing Iniciado
```
🔐 Iniciando proceso de pairing...
🔄 Forzando nuevo pairing...
📡 Reconectando a 10.1.1.53:12345...
```

### Código Recibido
```
🔐 Código de pairing recibido: A3F2B1
💡 Popup de pairing mostrado con código: A3F2B1
```

### Pairing Exitoso
```
✅ Pairing completado exitosamente!
📡 Estado de conexión: Pareado
```

### Popup Cerrado
```
❌ Popup de pairing cerrado
```

---

## 🎯 Tips y Best Practices

### ✅ DO's

- **Espera a que aparezca el código** antes de ir al terminal
- **Verifica el código dos veces** antes de ingresarlo
- **Guarda una captura de pantalla** del código si lo necesitas
- **Usa "Reintentar"** si el código no funciona la primera vez

### ❌ DON'Ts

- **No cierres el popup** antes de ingresar el código en el terminal
- **No reinicies la app** durante el proceso de pairing
- **No modifies config.json** manualmente durante el pairing
- **No uses códigos viejos** - siempre genera uno nuevo con "Reintentar"

---

## 🔍 Archivos Relevantes

### UI
- `windows/UI/MainWindow.xaml` - Popup de pairing (líneas 428-519)
- `windows/UI/MainWindow.xaml.cs` - Lógica del popup (líneas 430-510)

### Servicios
- `windows/Services/CloverWebSocketService.cs` - Manejo de mensajes de pairing
  - `HandlePairingCodeAsync()` - Procesa código recibido
  - `HandlePairingResponseAsync()` - Procesa token de autenticación

### Eventos
- `PairingCodeReceived` - Se dispara cuando llega un código
- `StateChanged` - Notifica cambios de estado (incluye Paired)

---

## 📞 Soporte

Si tienes problemas con el pairing:

1. **Revisa esta guía** completa
2. **Consulta los logs** en la pestaña "Logs"
3. **Verifica la conexión** de red entre PC y terminal
4. **Prueba "Reintentar"** en el popup
5. **Reinicia la aplicación** si persiste el problema

---

**Última actualización:** 16/01/2026
**Versión:** 1.0.0-alpha
