# Actualización: Monitoreo en Tiempo Real y Contador de Timeout

## Fecha: 18 de enero de 2026

## Cambios Implementados

### 1. Campo de Tiempo Restante en TransactionFile

Se agregó un nuevo campo para rastrear el tiempo restante del timeout:

```csharp
[JsonPropertyName("timeoutRemainingSeconds")]
public int? TimeoutRemainingSeconds { get; set; }
```

Este campo:
- Se inicializa en 120 cuando comienza el proceso
- Se actualiza cada segundo durante el countdown
- Se limpia (null) cuando la transacción finaliza

### 2. Actualización Periódica en OUTBOX

**InboxWatcherService.cs** - Método `CountdownWithUpdatesAsync`:
- Contador regresivo de 120 a 0 segundos
- Actualiza el archivo en OUTBOX cada 5 segundos
- Permite monitorear el progreso de transacciones en proceso

```csharp
Flujo:
120s → escribe OUTBOX
115s → escribe OUTBOX
110s → escribe OUTBOX
...
  5s → escribe OUTBOX
  0s → TIMEOUT
```

### 3. Monitor en Tiempo Real en la UI

**ProductionMainWindow.xaml.cs** - `_outboxMonitorTimer`:
- Timer que se ejecuta cada 2 segundos
- Lee archivos recientes del OUTBOX (últimos 130 segundos)
- Actualiza automáticamente las transacciones en la vista

**Características del Monitor:**
- Detecta archivos nuevos y los agrega a la lista
- Actualiza transacciones existentes con su estado actual
- Muestra el contador en tiempo real: `⏱️ Procesando (87s)`
- No duplica transacciones (busca por TransactionId)

### 4. Estados Mejorados en la UI

La UI ahora muestra:
- **Procesando con contador**: `⏱️ Procesando (87s)` - actualizado cada 2 segundos
- **Exitoso**: `✅ Exitoso` - Transacción completada exitosamente
- **Cancelado**: `❌ Cancelado` - Cancelado por el usuario
- **Timeout**: `⏱️ Timeout` - Sin respuesta del terminal (120s)
- **Fondos Insuficientes**: `💳 Sin fondos`
- **Fallido**: `❌ Fallido` - Error general

### 5. Flujo Completo de una Transacción

#### Escenario: Usuario hace un pago

**Tiempo 0s:**
```json
{
  "status": "Processing",
  "timeoutRemainingSeconds": 120,
  "transactionLog": [
    {"eventType": "RECEIVED", "description": "Transacción recibida"},
    {"eventType": "SENT_TO_TERMINAL", "description": "Enviado a terminal"}
  ]
}
```
UI muestra: `⏱️ Procesando (120s)`

**Tiempo 5s:**
```json
{
  "status": "Processing",
  "timeoutRemainingSeconds": 115
}
```
UI muestra: `⏱️ Procesando (115s)`

**Tiempo 10s:**
```json
{
  "status": "Processing",
  "timeoutRemainingSeconds": 110
}
```
UI muestra: `⏱️ Procesando (110s)`

**Tiempo 25s:** (Usuario pasa tarjeta)
```json
{
  "status": "Successful",
  "timeoutRemainingSeconds": null,
  "paymentInfo": {
    "cloverPaymentId": "ABC123",
    "cardLast4": "4242",
    "totalAmount": 100.50
  }
}
```
UI muestra: `✅ Exitoso - Transacción completada exitosamente`

#### Escenario: Timeout

**Tiempo 0s → 115s:** Actualizaciones cada 5 segundos
```
120s → 115s → 110s → 105s → ... → 5s
```

**Tiempo 120s:** (Sin respuesta)
```json
{
  "status": "Timeout",
  "timeoutRemainingSeconds": null,
  "errorMessage": "Timeout de 120 segundos - Sin respuesta del terminal"
}
```
UI muestra: `⏱️ Timeout - Sin respuesta del terminal`

### 6. Ventajas del Nuevo Sistema

#### Para el Usuario:
- ✅ Ve el progreso en tiempo real
- ✅ Sabe exactamente cuánto tiempo queda
- ✅ No necesita refrescar manualmente
- ✅ Identifica rápidamente transacciones problemáticas

#### Para el Sistema:
- ✅ Estado siempre actualizado en OUTBOX
- ✅ No se pierden transacciones
- ✅ Cancelaciones se registran correctamente
- ✅ Timeouts bien diferenciados de cancelaciones

#### Para Debugging:
- ✅ Trazabilidad completa con timestamps
- ✅ Log de eventos detallado
- ✅ Estados intermedios guardados
- ✅ Fácil identificar cuellos de botella

### 7. Ejemplo de Archivo OUTBOX Durante Proceso

**INV-001_processing_20260118_143000.json:**
```json
{
  "transactionId": "abc-123",
  "invoiceNumber": "INV-001",
  "amount": 100.50,
  "status": "Processing",
  "timeoutRemainingSeconds": 87,
  "processStartTime": "2026-01-18T14:30:00Z",
  "sentToTerminalTime": "2026-01-18T14:30:02Z",
  "transactionLog": [
    {
      "timestamp": "2026-01-18T14:30:00Z",
      "eventType": "RECEIVED",
      "description": "Transacción recibida en INBOX"
    },
    {
      "timestamp": "2026-01-18T14:30:02Z",
      "eventType": "SENT_TO_TERMINAL",
      "description": "Solicitud enviada a terminal Clover"
    }
  ]
}
```

### 8. Configuración del Monitor

**Intervalos de actualización:**
- OUTBOX se actualiza: Cada 5 segundos
- UI lee OUTBOX: Cada 2 segundos
- Contador decrementa: Cada 1 segundo

**Archivos considerados:**
- Últimos 130 segundos (timeout de 120s + 10s buffer)
- Solo archivos .json en OUTBOX
- Ordenados por fecha de modificación

### 9. Archivos Modificados en Esta Actualización

- ✅ `Models/TransactionModels.cs`: Campo `TimeoutRemainingSeconds`
- ✅ `Services/InboxWatcherService.cs`: Método `CountdownWithUpdatesAsync`
- ✅ `UI/ProductionMainWindow.xaml.cs`: Timer `_outboxMonitorTimer` y método `MonitorOutboxTransactions`

### 10. Testing Recomendado

1. **Transacción Normal (< 30s)**
   - Verificar que el contador se muestra
   - Confirmar que se actualiza cada 2 segundos en UI
   - Validar que cambia a "Exitoso" al completar

2. **Transacción Lenta (60-90s)**
   - Observar el contador bajando
   - Verificar archivos en OUTBOX actualizándose
   - Confirmar que completa correctamente

3. **Timeout (120s)**
   - Dejar que llegue a 0 segundos
   - Verificar que cambia a "Timeout"
   - Confirmar mensaje de error correcto

4. **Cancelación Manual**
   - Cancelar en el terminal antes de 120s
   - Verificar que cambia a "Cancelado"
   - Confirmar que el contador se detiene

5. **Múltiples Transacciones**
   - Varias transacciones simultáneas
   - Verificar que cada una tiene su contador
   - Confirmar que no se duplican en la lista

### 11. Logs del Sistema

Durante el proceso verás logs como:
```
🚀 Transacción recibida: INV-001
📤 Enviado a terminal: $100.50
⏱️  Contador: 115s restantes
⏱️  Contador: 110s restantes
⏱️  Contador: 105s restantes
✅ Respuesta recibida
💾 Estado guardado: Successful
🗑️ Archivo eliminado de INBOX
```

### 12. Mejoras Futuras Opcionales

1. **Barra de Progreso Visual**: Mostrar barra en lugar de solo segundos
2. **Notificaciones**: Alertas cuando hay timeout
3. **Sonidos**: Beep cuando completa o falla
4. **Gráficos**: Timeline de transacciones del día
5. **Filtros**: Filtrar por estado en la UI

---

## Compilación

✅ Compilado exitosamente sin errores
⚠️ 13 advertencias menores (referencias nulas - no críticas)

El sistema ahora actualiza los estados en tiempo real y muestra un contador visible de los 120 segundos de timeout.
