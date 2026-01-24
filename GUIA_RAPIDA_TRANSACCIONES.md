# 🚀 Guía Rápida: Sistema de Transacciones OUTBOX

## Inicio Rápido (5 minutos)

### 1️⃣ Copiar Archivos de Prueba
```powershell
.\test-outbox.ps1
```

### 2️⃣ Ejecutar Prueba
```powershell
.\run-test-outbox.ps1
```

### 3️⃣ Ver Resultados
Verás un análisis completo de las transacciones en OUTBOX.

---

## Uso en Tu Código

### Inicializar Servicio
```csharp
var configService = new ConfigurationService();
var outboxService = new TransactionOutboxService(configService);
```

### Casos Comunes

#### ¿La transacción está completada?
```csharp
bool completada = await outboxService.IsTransactionCompletedAsync("FB-12345");
if (completada)
{
    Console.WriteLine("¡Pago procesado!");
}
```

#### ¿Está pendiente?
```csharp
bool pendiente = await outboxService.IsTransactionPendingAsync("FB-12345");
if (pendiente)
{
    Console.WriteLine("Esperando respuesta del terminal...");
}
```

#### Ver Estado Actual
```csharp
var status = await outboxService.GetTransactionStatusAsync("FB-12345");
Console.WriteLine($"Estado: {status}");
// Output: Estado: Successful
```

#### Ver Historial Completo
```csharp
var historial = await outboxService.GetTransactionHistoryAsync("FB-12345");
foreach (var tx in historial)
{
    Console.WriteLine($"{tx.Timestamp}: {tx.Status}");
}
```

#### Estadísticas del Día
```csharp
var stats = await outboxService.GetDailyStatsAsync();
Console.WriteLine($"Hoy: {stats.SuccessfulTransactions} exitosas de {stats.TotalTransactions}");
Console.WriteLine($"Total recaudado: ${stats.TotalAmount}");
```

---

## Estados de Transacción

| Estado | Valor | Descripción |
|--------|-------|-------------|
| **Pending** | 0 | ⏳ Enviada, esperando respuesta |
| **Processing** | 1 | 🔄 Procesándose en terminal |
| **Successful** | 2 | ✅ Completada exitosamente |
| **Cancelled** | 3 | ❌ Cancelada por usuario |
| **Timeout** | 4 | ⏱️ Sin respuesta (120s) |
| **InsufficientFunds** | 5 | 💳 Fondos insuficientes |
| **Failed** | 6 | ⚠️ Error de procesamiento |

---

## Formato de Archivos

```
{invoiceNumber}_{status}_{timestamp}.json
```

**Ejemplo:**
```
FB-12345-12345678_pending_20260119_082747_918.json
FB-12345-12345678_successful_20260119_082748_756.json
```

---

## Contenido de Archivo

```json
{
  "transactionId": "2bededf3-9c75-4fb8-a7ee-6adc19a0d6d3",
  "externalId": "TXN-20260119082702",
  "timestamp": "2026-01-19T11:27:48.7564942Z",
  "status": 2,
  "type": "SALE",
  "invoiceNumber": "FB-12345-12345678",
  "amount": 50.00,
  "customerName": "Cliente POS",
  "notes": "Producto Test 1 x1 + Producto Test 2 x1",
  "paymentInfo": {
    "totalAmount": 50.00,
    "terminalTimeoutDefault": 120,
    "processingStartTime": "2026-01-19T08:27:47.9167852-03:00"
  }
}
```

---

## Ejemplos Completos

### Monitorear Transacción
```csharp
string factura = "FB-12345-12345678";

// Loop de monitoreo
while (true)
{
    var status = await outboxService.GetTransactionStatusAsync(factura);
    
    if (status == TransactionStatus.Successful)
    {
        Console.WriteLine("✅ ¡Pago exitoso!");
        break;
    }
    else if (status == TransactionStatus.Cancelled)
    {
        Console.WriteLine("❌ Pago cancelado");
        break;
    }
    else if (status == TransactionStatus.Pending)
    {
        Console.WriteLine("⏳ Esperando...");
        await Task.Delay(1000); // Esperar 1 segundo
    }
    else
    {
        Console.WriteLine($"⚠️ Estado: {status}");
        break;
    }
}
```

### Generar Reporte
```csharp
var stats = await outboxService.GetDailyStatsAsync();

Console.WriteLine("=== REPORTE DIARIO ===");
Console.WriteLine($"Fecha: {DateTime.Now:yyyy-MM-dd}");
Console.WriteLine();
Console.WriteLine($"Total Transacciones: {stats.TotalTransactions}");
Console.WriteLine($"  ✅ Exitosas: {stats.SuccessfulTransactions}");
Console.WriteLine($"  ⏳ Pendientes: {stats.PendingTransactions}");
Console.WriteLine($"  ❌ Canceladas: {stats.CancelledTransactions}");
Console.WriteLine($"  ⚠️ Fallidas: {stats.FailedTransactions}");
Console.WriteLine();
Console.WriteLine($"Monto Total: ${stats.TotalAmount:N2}");
Console.WriteLine($"Ticket Promedio: ${stats.AverageAmount:N2}");
```

---

## Archivos Importantes

| Archivo | Descripción |
|---------|-------------|
| **TransactionOutboxService.cs** | Servicio principal |
| **TransactionExamples.cs** | 7 ejemplos de uso |
| **TestOutboxReader.cs** | Programa de prueba |
| **TRANSACCIONES_OUTBOX_IMPLEMENTACION.md** | Documentación completa |

---

## Comandos Útiles

```powershell
# Compilar
dotnet build Cloverwin.sln

# Ejecutar test
.\run-test-outbox.ps1

# Ver archivos OUTBOX
Get-ChildItem bin\Debug\net8.0-windows\OUTBOX\*.json

# Leer archivo específico
Get-Content bin\Debug\net8.0-windows\OUTBOX\FB-12345-12345678_successful_*.json | ConvertFrom-Json
```

---

## Troubleshooting

### No se encuentran transacciones
```csharp
var config = configService.GetConfig();
Console.WriteLine($"OUTBOX Path: {config.Folders.Outbox}");
```

### Ver todas las transacciones
```csharp
var all = await outboxService.ReadAllTransactionsFromOutboxAsync();
Console.WriteLine($"Total: {all.Count}");
```

### Verificar por estado
```csharp
var byStatus = await outboxService.GetTransactionsByStatusAsync();
foreach (var group in byStatus)
{
    Console.WriteLine($"{group.Key}: {group.Value.Count}");
}
```

---

## ¿Necesitas Ayuda?

1. Ver **TRANSACCIONES_OUTBOX_IMPLEMENTACION.md** - Documentación completa
2. Ver **TransactionExamples.cs** - 7 ejemplos prácticos
3. Ejecutar **TestOutboxReader** - Prueba todas las funcionalidades

---

**¡Listo para usar!** 🎉
