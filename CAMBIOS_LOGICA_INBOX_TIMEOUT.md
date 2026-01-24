# Cambios en la Lógica del Sistema - Gestión de INBOX y Timeout

## Fecha: 18 de enero de 2026

## Resumen de Cambios Implementados

Se ha modificado la lógica del sistema para mejorar el manejo de transacciones, mantener archivos en INBOX hasta completar el proceso de pago, implementar timeout de 120 segundos, y registrar un historial detallado de todas las transacciones.

---

## 1. Cambios en el Modelo de Datos

### `TransactionModels.cs`

#### **Nuevos campos en `TransactionFile`:**

- **`ProcessStartTime`** (DateTime?): Tiempo de inicio del proceso
- **`ProcessEndTime`** (DateTime?): Tiempo de finalización del proceso
- **`SentToTerminalTime`** (DateTime?): Momento exacto en que se envió al terminal
- **`InboxFilePath`** (string?): Ruta del archivo original en INBOX
- **`TransactionLog`** (List<TransactionLogEntry>): Lista de eventos de la transacción

#### **Nueva clase `TransactionLogEntry`:**
```csharp
public class TransactionLogEntry
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; }
    public string Description { get; set; }
    public string? Details { get; set; }
}
```

#### **Método agregado a `TransactionFile`:**
```csharp
public void AddLogEntry(string eventType, string description, string? details = null)
```
Permite agregar entradas al log de eventos de forma sencilla.

#### **Nuevo estado en `TransactionStatus`:**
- **`Timeout`**: Agregado para diferenciar entre cancelación manual y timeout automático

---

## 2. Cambios en `InboxWatcherService.cs`

### **Nuevas características:**

1. **Gestión de Transacciones Activas:**
   - Diccionario `_activeTransactions` para rastrear transacciones en proceso
   - Diccionario `_transactionTimeouts` para gestionar CancellationTokens

2. **Lógica de Timeout de 120 Segundos:**
   - Los archivos permanecen en INBOX durante todo el proceso
   - Se implementa un timeout de 120 segundos usando `Task.WhenAny`
   - Si no hay respuesta en 120s, la transacción se marca como `Timeout`

3. **Proceso Mejorado:**

```csharp
// Antes:
- Leer archivo de INBOX
- Procesar pago
- Eliminar archivo de INBOX inmediatamente
- Guardar resultado en OUTBOX

// Ahora:
- Leer archivo de INBOX (mantenerlo)
- Registrar transacción activa
- Iniciar timeout de 120 segundos
- Enviar a terminal Clover
- Esperar respuesta O timeout (lo que ocurra primero)
- Registrar en log histórico
- Guardar resultado en OUTBOX
- Eliminar archivo de INBOX SOLO cuando todo está completo
```

4. **Registro de Eventos:**
   - Cada transacción registra eventos detallados:
     - `RECEIVED`: Archivo recibido en INBOX
     - `PROCESSING`: Inicio del proceso
     - `SENT_TO_TERMINAL`: Enviado a terminal
     - `RESPONSE_RECEIVED`: Respuesta recibida
     - `TIMEOUT`: Timeout alcanzado
     - `RESULT_PROCESSED`: Resultado procesado
     - `FINALIZED`: Transacción finalizada

5. **Nuevo Método:**
```csharp
public object GetActiveTransactionsStatus()
```
Permite consultar el estado de transacciones activas y su tiempo transcurrido.

---

## 3. Nuevo Servicio: `TransactionLogService.cs`

### **Funcionalidad:**

Servicio dedicado para mantener un registro histórico de todas las transacciones procesadas.

### **Características:**

1. **`LogTransactionAsync`**: Registra cada transacción en archivos JSONL (JSON Lines)
   - Un archivo por día: `transactions_YYYYMMDD.jsonl`
   - Ubicación: `ARCHIVE/TransactionLog/`
   - Incluye toda la información de la transacción y su log de eventos

2. **`GetTodaysSummaryAsync`**: Obtiene resumen del día actual
   - Total de transacciones
   - Conteo por estado (exitosas, fallidas, canceladas, timeout, fondos insuficientes)
   - Monto total procesado
   - Tiempo promedio de procesamiento

3. **`SearchByInvoiceNumberAsync`**: Busca transacciones por número de factura
   - Busca en los últimos N días (por defecto 7)
   - Retorna todas las ocurrencias

### **Modelo `TransactionLogRecord`:**
```csharp
- TransactionId
- InvoiceNumber
- Amount, Status, Type
- Tiempos: ReceivedTime, ProcessStartTime, SentToTerminalTime, ProcessEndTime
- TotalProcessingSeconds (calculado)
- PaymentInfo (si exitoso)
- ErrorMessage/ErrorCode (si falló)
- TransactionLog (lista de eventos)
- CustomerName, Notes
```

---

## 4. Cambios en `TransactionFileService.cs`

### **Método `ProcessPaymentResult` actualizado:**

- Diferencia entre `Cancelled` y `Timeout`
- Registra entradas de log según el resultado:
  - `PAYMENT_SUCCESS`: Pago exitoso
  - `CANCELLED`: Cancelado por usuario
  - `TIMEOUT`: Timeout alcanzado
  - `INSUFFICIENT_FUNDS`: Fondos insuficientes
  - `FAILED`: Fallo general

---

## 5. Flujo Completo de una Transacción

### **Escenario Normal (Pago Exitoso):**

1. Archivo `invoice_001.json` llega a INBOX
2. Sistema detecta el archivo y lo lee
3. Se registra en transacciones activas
4. Se inicia timeout de 120 segundos
5. Log: `RECEIVED` → `PROCESSING` → `SENT_TO_TERMINAL`
6. Terminal procesa el pago (usuario pasa tarjeta)
7. Respuesta recibida en 30 segundos
8. Log: `RESPONSE_RECEIVED` → `RESULT_PROCESSED` → `FINALIZED`
9. Se guarda en OUTBOX: `invoice_001_successful_20260118_143022.json`
10. Se registra en log histórico: `ARCHIVE/TransactionLog/transactions_20260118.jsonl`
11. Se elimina de INBOX
12. Duración total: 30 segundos

### **Escenario Timeout:**

1. Archivo `invoice_002.json` llega a INBOX
2. Sistema detecta el archivo y lo lee
3. Se registra en transacciones activas
4. Se inicia timeout de 120 segundos
5. Log: `RECEIVED` → `PROCESSING` → `SENT_TO_TERMINAL`
6. Terminal NO responde (problema de red, terminal apagado, etc.)
7. Después de 120 segundos se alcanza el timeout
8. Log: `TIMEOUT` → `FINALIZED`
9. Se guarda en OUTBOX: `invoice_002_timeout_20260118_143202.json`
10. Se registra en log histórico con estado `Timeout`
11. Se elimina de INBOX
12. Duración total: 120 segundos

### **Escenario Cancelación:**

1. Archivo llega a INBOX
2. Proceso normal hasta terminal
3. Usuario presiona "Cancelar" en el terminal
4. Respuesta recibida con estado "CANCELLED"
5. Se guarda en OUTBOX con estado `Cancelled`
6. Log incluye eventos completos
7. Duración: tiempo hasta cancelación

---

## 6. Estructura de Archivos en OUTBOX

Los archivos en OUTBOX ahora incluyen información completa:

```json
{
  "transactionId": "abc-123-def",
  "invoiceNumber": "INV-001",
  "amount": 100.50,
  "status": "Successful", // o Timeout, Cancelled, Failed, etc.
  "timestamp": "2026-01-18T14:30:00Z",
  "processStartTime": "2026-01-18T14:30:00Z",
  "sentToTerminalTime": "2026-01-18T14:30:02Z",
  "processEndTime": "2026-01-18T14:30:25Z",
  "paymentInfo": {
    "cloverPaymentId": "...",
    "cardLast4": "1234",
    "totalAmount": 100.50
  },
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
    },
    {
      "timestamp": "2026-01-18T14:30:25Z",
      "eventType": "RESPONSE_RECEIVED",
      "description": "Respuesta recibida de terminal"
    },
    {
      "timestamp": "2026-01-18T14:30:25Z",
      "eventType": "FINALIZED",
      "description": "Transacción finalizada con estado: Successful",
      "details": "Duración: 25.00s"
    }
  ]
}
```

---

## 7. Beneficios de los Cambios

### **Para el Sistema:**
1. **Mayor robustez**: Los archivos no se pierden durante el procesamiento
2. **Trazabilidad completa**: Cada transacción tiene un log detallado de eventos
3. **Gestión de timeouts**: Evita transacciones colgadas indefinidamente
4. **Histórico persistente**: Todas las transacciones se registran para auditoría

### **Para Debugging:**
1. Ver exactamente qué sucedió con cada transacción
2. Identificar cuellos de botella en el proceso
3. Analizar tiempos de respuesta del terminal
4. Detectar patrones de fallo

### **Para Reporting:**
1. Resúmenes diarios automáticos
2. Estadísticas de éxito/fallo
3. Tiempos promedio de procesamiento
4. Búsqueda rápida por número de factura

---

## 8. Configuración

El timeout de 120 segundos está hardcodeado en el código. Si se desea cambiar:

**Ubicación**: `InboxWatcherService.cs`, línea ~130
```csharp
var timeoutTask = Task.Delay(120000, timeoutCts.Token); // 120 segundos = 120000 ms
```

Para hacerlo configurable, se puede agregar al `appsettings.json`:
```json
{
  "Transaction": {
    "TimeoutMs": 120000,
    "InboxRetentionSeconds": 120
  }
}
```

---

## 9. Testing Recomendado

### **Casos de Prueba:**

1. **Transacción Normal**: Verificar que se procese correctamente
2. **Timeout Simulado**: Desconectar terminal antes de enviar
3. **Cancelación**: Presionar cancelar en terminal
4. **Múltiples Transacciones**: Varios archivos simultáneos
5. **Fondos Insuficientes**: Tarjeta sin fondos
6. **Archivo Inválido**: JSON malformado

### **Verificaciones:**

- ✅ Archivos permanecen en INBOX durante procesamiento
- ✅ Archivos se eliminan solo al finalizar
- ✅ OUTBOX contiene resultado con log completo
- ✅ Log histórico se crea correctamente
- ✅ Timeout funciona después de 120 segundos
- ✅ Estados se registran correctamente

---

## 10. Archivos Modificados

- ✅ `Models/TransactionModels.cs`: Nuevos campos y clase TransactionLogEntry
- ✅ `Services/InboxWatcherService.cs`: Lógica de timeout y retención en INBOX
- ✅ `Services/TransactionFileService.cs`: Procesamiento mejorado de resultados
- ✅ `Services/TransactionLogService.cs`: **NUEVO** - Registro histórico

---

## 11. Próximos Pasos Opcionales

1. **Dashboard**: Crear UI para ver el resumen de transacciones del día
2. **Alertas**: Notificar cuando hay muchos timeouts
3. **Configuración**: Hacer el timeout configurable desde UI
4. **Reintentos**: Implementar lógica de reintento para timeouts
5. **Exportación**: Exportar logs históricos a CSV/Excel

---

## Conclusión

El sistema ahora tiene:
- ✅ Archivos en INBOX protegidos hasta completar el proceso
- ✅ Timeout de 120 segundos implementado
- ✅ Registro detallado de eventos por transacción
- ✅ Historial completo en log persistente
- ✅ Mejor trazabilidad y debugging
- ✅ Distinción clara entre estados (Timeout vs Cancelled vs Failed)

La implementación está completa y lista para usar. El sistema compiló exitosamente sin errores.
