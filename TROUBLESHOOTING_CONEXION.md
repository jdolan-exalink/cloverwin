# 🔧 Guía de Troubleshooting - CloverBridge UI

## ✅ Aplicación Actualizada y Funcionando

La aplicación C# con UI completa está ahora ejecutándose correctamente con las siguientes mejoras:

### 🆕 Mejoras Implementadas

1. ✅ **Manejo mejorado de SSL/TLS**

   - Soporte para certificados autofirmados
   - Opción de conexión segura (wss://) o no segura (ws://)

2. ✅ **Configuración completa en UI**

   - Campo Host (IP del terminal)
   - Campo Puerto
   - Remote Application ID
   - Serial Number
   - Auth Token
   - Checkbox Secure

3. ✅ **Mensajes de error mejorados**

   - Feedback detallado en caso de error de conexión
   - Logs informativos sobre configuración cargada
   - Instrucciones de troubleshooting en pantalla

4. ✅ **Inicialización correcta**
   - Servicios backend se inician antes de la UI
   - Thread STA correctamente configurado para WPF
   - Sincronización mejorada

## 🔍 Problemas Comunes y Soluciones

### Problema 1: "Error de Conexión" en la UI

**Síntomas:**

- Badge rojo con "Error de Conexión"
- Logs muestran: "No se puede conectar a..."

**Causas posibles:**

1. Terminal Clover no está encendido
2. Terminal no está en la misma red
3. IP del terminal es incorrecta
4. Network Pay Display no está habilitado

**Solución:**

1. **Verificar el Terminal Clover:**

   ```
   - Encender el terminal
   - Ir a Settings → Network Pay Display
   - Habilitar "Network Pay Display"
   - Anotar la IP mostrada (ej: 192.168.1.100)
   ```

2. **Actualizar configuración en la UI:**

   ```
   - Abrir tab "Configuración"
   - Host: [IP del terminal, ej: 192.168.1.100]
   - Puerto: 12345 (default)
   - Secure: ☐ (desmarcar para ws://)
   - Click "Guardar Configuración y Reintentar"
   - Reiniciar aplicación
   ```

3. **Verificar conectividad de red:**

   ```powershell
   # En PowerShell:
   Test-NetConnection -ComputerName [IP_DEL_TERMINAL] -Port 12345

   # Ejemplo:
   Test-NetConnection -ComputerName 192.168.1.100 -Port 12345
   ```

### Problema 2: "Pairing Requerido"

**Síntomas:**

- Badge naranja con "Pairing Requerido"
- Ventana de pairing no aparece automáticamente

**Solución:**

1. Click en botón "Pairing" en el header
2. Esperar código de 6 dígitos
3. Ingresar código en terminal Clover:
   - Settings → Network Pay Display → Enter Pairing Code
4. Confirmar en terminal

### Problema 3: Aplicación no inicia o se cierra inmediatamente

**Síntomas:**

- Proceso aparece y desaparece
- No se abre ventana

**Solución:**

1. **Ejecutar en modo consola para ver errores:**

   ```powershell
   cd d:\DEVs\Clover2\windows
   .\bin\Debug\net8.0-windows\CloverBridge.exe --console
   ```

2. **Revisar logs:**

   ```powershell
   notepad "$env:APPDATA\CloverBridge\logs\clover-bridge-$(Get-Date -Format 'yyyyMMdd').log"
   ```

3. **Recompilar si es necesario:**
   ```powershell
   cd d:\DEVs\Clover2\windows
   dotnet build CloverBridge.csproj --configuration Debug
   ```

### Problema 4: SSL/TLS Error (wss://)

**Síntomas:**

- Error: "The SSL connection could not be established"
- Error: "The remote certificate is invalid"

**Solución:**

**Opción A - Usar conexión no segura (recomendado para desarrollo):**

```
1. Ir a tab "Configuración"
2. Desmarcar "Usar conexión segura (wss://)"
3. Guardar y reiniciar
```

**Opción B - Aceptar certificado (ya implementado):**

- La aplicación ahora acepta certificados autofirmados automáticamente
- Si usa wss://, asegúrese que el terminal soporte SSL

### Problema 5: Inbox no procesa solicitudes

**Síntomas:**

- Archivos JSON se crean en inbox pero no se procesan
- No hay respuestas en outbox

**Solución:**

1. **Verificar carpetas:**

   ```powershell
   explorer "$env:APPDATA\CloverBridge"
   ```

   Debe ver: inbox/, outbox/, archive/

2. **Verificar permisos:**

   - La aplicación debe tener permisos de lectura/escritura
   - Ejecutar como administrador si es necesario

3. **Ver logs del InboxWatcher:**
   ```powershell
   Get-Content "$env:APPDATA\CloverBridge\logs\clover-bridge-$(Get-Date -Format 'yyyyMMdd').log" | Select-String "InboxWatcher"
   ```

## 📊 Verificación de Estado

### Verificar que la aplicación está ejecutándose:

```powershell
Get-Process -Name "CloverBridge" | Select-Object ProcessName, Id, @{Name='MemoryMB';Expression={[math]::Round($_.WorkingSet64/1MB,2)}}
```

**Resultado esperado:**

```
ProcessName     Id MemoryMB
-----------     -- --------
CloverBridge xxxxx   150-200
```

### Verificar logs en tiempo real:

```powershell
Get-Content "$env:APPDATA\CloverBridge\logs\clover-bridge-$(Get-Date -Format 'yyyyMMdd').log" -Wait -Tail 20
```

### Verificar configuración actual:

```powershell
Get-Content "$env:APPDATA\CloverBridge\config.json" | ConvertFrom-Json | ConvertTo-Json
```

## 🎯 Configuración Recomendada para Desarrollo

```json
{
  "clover": {
    "host": "192.168.1.100", // ← IP de tu terminal
    "port": 12345,
    "secure": false, // ← ws:// en lugar de wss://
    "authToken": null, // ← Se obtiene después del pairing
    "remoteAppId": "clover-bridge",
    "posName": "CloverBridge",
    "serialNumber": "CB-001"
  }
}
```

## 🔄 Reiniciar Aplicación

### Detener:

```powershell
Get-Process -Name "CloverBridge" -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Iniciar con UI:

```powershell
cd d:\DEVs\Clover2\windows
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

### Iniciar en modo bandeja:

```powershell
cd d:\DEVs\Clover2\windows
.\bin\Debug\net8.0-windows\CloverBridge.exe
```

## 📝 Checklist de Configuración Inicial

- [ ] Terminal Clover encendido y conectado a la red
- [ ] Network Pay Display habilitado en terminal
- [ ] IP del terminal anotada
- [ ] Terminal y PC en la misma red
- [ ] Firewall no bloquea puerto 12345
- [ ] Configuración actualizada en UI:
  - [ ] Host correcto
  - [ ] Puerto 12345
  - [ ] Secure = false
- [ ] Aplicación reiniciada después de cambiar configuración
- [ ] Estado muestra "Conectado" o "Pairing Requerido"
- [ ] Pairing completado (si es necesario)

## 🆘 Si Nada Funciona

1. **Reset completo de configuración:**

   ```powershell
   Remove-Item "$env:APPDATA\CloverBridge\config.json"
   # Reiniciar aplicación - creará config por defecto
   ```

2. **Limpiar logs y archivos:**

   ```powershell
   Remove-Item "$env:APPDATA\CloverBridge\logs\*"
   Remove-Item "$env:APPDATA\CloverBridge\inbox\*"
   Remove-Item "$env:APPDATA\CloverBridge\outbox\*"
   ```

3. **Recompilar desde cero:**

   ```powershell
   cd d:\DEVs\Clover2\windows
   dotnet clean
   dotnet build --configuration Debug
   ```

4. **Revisar documentación de Clover:**
   - https://docs.clover.com/docs/network-pay-display
   - https://docs.clover.com/docs/pairing-with-clover-devices

## ✅ Estado Actual

- ✅ Aplicación compilada sin errores
- ✅ UI ejecutándose correctamente
- ✅ Servicios backend iniciados
- ✅ Configuración editable desde UI
- ✅ Logs detallados disponibles
- ⚠️ Requiere terminal Clover físico para testing completo

---

**Última actualización:** 15 de Enero, 2026  
**Versión:** 1.0.0
