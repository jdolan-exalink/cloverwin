# Sistema de Gestión de Transacciones con OUTBOX

## Resumen

Se ha implementado un sistema completo para leer y analizar transacciones desde la carpeta OUTBOX. El sistema permite:

- ✅ **Campo de Factura**: El campo `invoiceNumber` ya existía en el modelo `TransactionFile`
- ✅ **Lectura de OUTBOX**: Nuevo servicio `TransactionOutboxService` para leer archivos
- ✅ **Manejo de Estados**: Soporta estados Pending, Processing, Successful, Cancelled, Timeout, Failed
- ✅ **Análisis de Historial**: Seguimiento completo de cambios de estado de transacciones
- ✅ **Estadísticas**: Cálculo de estadísticas diarias de transacciones

## Archivos Creados/Modificados

### Nuevos Archivos

1. **Services/TransactionOutboxService.cs**
   - Servicio especializado para leer archivos de OUTBOX
   - Métodos para análisis de transacciones
   - Generación de estadísticas

2. **TestOutboxReader.cs**
   - Programa de prueba para validar funcionalidades
   - Análisis completo de transacciones
   - Demostración de uso del servicio

3. **Scripts de Prueba**
   - `test-outbox.ps1`: Copia archivos de ejemplo a OUTBOX
   - `run-test-outbox.ps1`: Ejecuta el programa de prueba

### Archivos Modificados

1. **Program.cs**
   - Agregado flag `--test-outbox` para ejecutar pruebas
   - Integración con TestOutboxReader

## Funcionalidades Implementadas

### 1. Lectura de Transacciones

```csharp
// Leer todas las transacciones desde OUTBOX
var allTransactions = await outboxService.ReadAllTransactionsFromOutboxAsync();

// Leer transacciones de una factura específica
var transactions = await outboxService.ReadTransactionsByInvoiceAsync("FB-12345-12345678");
```

### 2. Análisis por Estado

```csharp
// Agrupar transacciones por estado
var byStatus = await outboxService.GetTransactionsByStatusAsync();

// Verificar si está pendiente
bool isPending = await outboxService.IsTransactionPendingAsync("FB-12345-12345678");

// Verificar si está completada
bool isCompleted = await outboxService.IsTransactionCompletedAsync("FB-12345-12345678");

// Obtener estado actual
TransactionStatus? status = await outboxService.GetTransactionStatusAsync("FB-12345-12345678");
```

### 3. Historial de Transacciones

```csharp
// Obtener historial completo (todos los estados)
var history = await outboxService.GetTransactionHistoryAsync("FB-12345-12345678");

// Analizar transacción con detalles
var analysis = await outboxService.AnalyzeTransactionAsync("FB-12345-12345678");
```

### 4. Estadísticas

```csharp
// Obtener estadísticas del día
var stats = await outboxService.GetDailyStatsAsync();

Console.WriteLine($"Total: {stats.TotalTransactions}");
Console.WriteLine($"Exitosas: {stats.SuccessfulTransactions}");
Console.WriteLine($"Monto Total: ${stats.TotalAmount:F2}");
Console.WriteLine($"Promedio: ${stats.AverageAmount:F2}");
```

## Estados de Transacción

El sistema maneja los siguientes estados:

- **Pending (0)**: Transacción enviada, esperando procesamiento
- **Processing (1)**: Procesándose en terminal Clover
- **Successful (2)**: Pago completado exitosamente
- **Cancelled (3)**: Cancelada por el usuario
- **Timeout (4)**: Timeout alcanzado (120 segundos)
- **InsufficientFunds (5)**: Fondos insuficientes / tarjeta rechazada
- **Failed (6)**: Error durante procesamiento

## Formato de Archivos OUTBOX

Los archivos se nombran con el formato:
```
{invoiceNumber}_{status}_{timestamp}.json
```

Ejemplo:
```
FB-12345-12345678_pending_20260119_082747_918.json
FB-12345-12345678_successful_20260119_082748_756.json
```

## Ejemplo de Uso

### Archivos de Prueba Incluidos

Se incluyen 2 archivos de ejemplo de una transacción exitosa:

1. **FB-12345-12345678_pending_20260119_082747_918.json**
   - Estado: Pending (0)
   - Monto: $50.00
   - Timestamp: 2026-01-19 11:27:47

2. **FB-12345-12345678_successful_20260119_082748_756.json**
   - Estado: Successful (2)
   - Monto: $50.00
   - Timestamp: 2026-01-19 11:27:48

### Ejecutar Pruebas

```powershell
# 1. Preparar archivos de prueba
.\test-outbox.ps1

# 2. Ejecutar test de lectura
.\run-test-outbox.ps1

# O directamente:
.\bin\Debug\net8.0-windows\CloverBridge.exe --test-outbox
```

### Salida Esperada

El test mostrará:
- Lista de todas las transacciones en OUTBOX
- Transacciones agrupadas por estado
- Estadísticas del día
- Análisis detallado de transacciones específicas
- Historial completo de cambios de estado

## Modelos de Datos

### TransactionFile

```csharp
public class TransactionFile
{
    public string TransactionId { get; set; }
    public string ExternalId { get; set; }
    public DateTime Timestamp { get; set; }
    public TransactionStatus Status { get; set; }
    public string Type { get; set; } // SALE, REFUND, VOID
    public string InvoiceNumber { get; set; } // ✅ Campo de factura
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
    public PaymentFileInfo? PaymentInfo { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TransactionLogEntry> TransactionLog { get; set; }
}
```

### TransactionStats

```csharp
public class TransactionStats
{
    public int TotalTransactions { get; set; }
    public int SuccessfulTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public int CancelledTransactions { get; set; }
    public int FailedTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageAmount { get; set; }
}
```

### TransactionAnalysis

```csharp
public class TransactionAnalysis
{
    public string InvoiceNumber { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
    public int TotalStates { get; set; }
    public TransactionStatus CurrentStatus { get; set; }
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? PaymentDetails { get; set; }
    public string? ProcessingDuration { get; set; }
    public List<StateInfo> States { get; set; }
}
```

## Integración con Sistema Existente

### TransactionFileService

El servicio existente ya incluye métodos para:
- Escribir transacciones a OUTBOX: `WriteTransactionToOutboxAsync()`
- Leer última transacción: `ReadLatestTransactionFromOutboxAsync()`
- Actualizar estados: `UpdateTransactionStatus()`
- Procesar resultados de pago: `ProcessPaymentResult()`

### TransactionOutboxService

El nuevo servicio complementa con:
- Lectura masiva de transacciones
- Análisis y agrupación por estado
- Generación de estadísticas
- Historial completo de transacciones

## Próximos Pasos

1. **Integración con UI**: Mostrar estadísticas en tiempo real
2. **Dashboard de Transacciones**: Panel con estados y métricas
3. **Alertas**: Notificaciones para transacciones fallidas
4. **Reportes**: Generación de reportes diarios/semanales
5. **Limpieza Automática**: Archivado automático de transacciones antiguas

## Notas Técnicas

- Todos los métodos son asíncronos (`async/await`)
- Manejo de errores con logging (Serilog)
- Serialización JSON con camelCase
- Ordenamiento cronológico de transacciones
- Soporte para archivos grandes (lectura por demanda)

## Resumen de Cambios

✅ Campo `invoiceNumber` ya existía en el modelo
✅ Nuevo servicio `TransactionOutboxService` implementado
✅ Lectura completa de archivos OUTBOX
✅ Manejo de estados Pending y Successful (y todos los demás)
✅ Análisis de historial de transacciones
✅ Cálculo de estadísticas
✅ Programa de prueba funcional
✅ Scripts de testing incluidos
✅ Compilación exitosa

**Estado**: ✅ **COMPLETADO**
