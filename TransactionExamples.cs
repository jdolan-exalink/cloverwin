using CloverBridge.Services;
using CloverBridge.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CloverBridge.Examples;

/// <summary>
/// Ejemplos de uso del sistema de transacciones OUTBOX
/// </summary>
public class TransactionExamples
{
    private readonly TransactionOutboxService _outboxService;
    private readonly ConfigurationService _configService;

    public TransactionExamples()
    {
        _configService = new ConfigurationService();
        _outboxService = new TransactionOutboxService(_configService);
    }

    /// <summary>
    /// Ejemplo 1: Verificar estado de una transacción
    /// </summary>
    public async Task Example1_CheckTransactionStatus()
    {
        Console.WriteLine("=== Ejemplo 1: Verificar Estado de Transacción ===\n");

        string invoiceNumber = "FB-12345-12345678";

        // Verificar si está pendiente
        bool isPending = await _outboxService.IsTransactionPendingAsync(invoiceNumber);
        Console.WriteLine($"¿Está pendiente? {(isPending ? "SÍ" : "NO")}");

        // Verificar si está completada
        bool isCompleted = await _outboxService.IsTransactionCompletedAsync(invoiceNumber);
        Console.WriteLine($"¿Está completada? {(isCompleted ? "SÍ" : "NO")}");

        // Obtener estado actual
        var status = await _outboxService.GetTransactionStatusAsync(invoiceNumber);
        Console.WriteLine($"Estado actual: {status}\n");
    }

    /// <summary>
    /// Ejemplo 2: Ver historial de una transacción
    /// </summary>
    public async Task Example2_ViewTransactionHistory()
    {
        Console.WriteLine("=== Ejemplo 2: Ver Historial de Transacción ===\n");

        string invoiceNumber = "FB-12345-12345678";

        var history = await _outboxService.GetTransactionHistoryAsync(invoiceNumber);

        if (history.Count == 0)
        {
            Console.WriteLine("No se encontró historial para esta factura.");
            return;
        }

        Console.WriteLine($"Historial de factura {invoiceNumber}:");
        Console.WriteLine($"Total de estados: {history.Count}\n");

        foreach (var transaction in history)
        {
            Console.WriteLine($"[{transaction.Timestamp:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"  Estado: {transaction.Status}");
            Console.WriteLine($"  Monto: ${transaction.Amount}");
            if (!string.IsNullOrEmpty(transaction.ErrorMessage))
            {
                Console.WriteLine($"  Error: {transaction.ErrorMessage}");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Ejemplo 3: Análisis detallado de una transacción
    /// </summary>
    public async Task Example3_DetailedAnalysis()
    {
        Console.WriteLine("=== Ejemplo 3: Análisis Detallado ===\n");

        string invoiceNumber = "FB-12345-12345678";

        var analysis = await _outboxService.AnalyzeTransactionAsync(invoiceNumber);

        if (analysis.Status != "OK")
        {
            Console.WriteLine($"Error: {analysis.Message}");
            return;
        }

        Console.WriteLine($"Factura: {analysis.InvoiceNumber}");
        Console.WriteLine($"Estado Actual: {analysis.CurrentStatus}");
        Console.WriteLine($"Monto: ${analysis.Amount}");
        Console.WriteLine($"Cliente: {analysis.CustomerName ?? "N/A"}");
        Console.WriteLine($"Duración de Procesamiento: {analysis.ProcessingDuration ?? "N/A"}");
        
        if (!string.IsNullOrEmpty(analysis.PaymentDetails))
        {
            Console.WriteLine($"Detalles de Pago: {analysis.PaymentDetails}");
        }

        Console.WriteLine($"\nProgresión de Estados ({analysis.States.Count} cambios):");
        foreach (var state in analysis.States)
        {
            Console.WriteLine($"  {state.Timestamp:HH:mm:ss.fff} -> {state.Status}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Ejemplo 4: Ver todas las transacciones por estado
    /// </summary>
    public async Task Example4_ViewTransactionsByStatus()
    {
        Console.WriteLine("=== Ejemplo 4: Transacciones por Estado ===\n");

        var byStatus = await _outboxService.GetTransactionsByStatusAsync();

        foreach (var group in byStatus)
        {
            if (group.Value.Count > 0)
            {
                Console.WriteLine($"{group.Key}: {group.Value.Count} transacciones");
                
                // Mostrar primeras 3
                foreach (var tx in group.Value.Take(3))
                {
                    Console.WriteLine($"  - {tx.InvoiceNumber} | ${tx.Amount} | {tx.Timestamp:yyyy-MM-dd HH:mm:ss}");
                }
                
                if (group.Value.Count > 3)
                {
                    Console.WriteLine($"  ... y {group.Value.Count - 3} más");
                }
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Ejemplo 5: Estadísticas del día
    /// </summary>
    public async Task Example5_DailyStatistics()
    {
        Console.WriteLine("=== Ejemplo 5: Estadísticas del Día ===\n");

        var stats = await _outboxService.GetDailyStatsAsync();

        Console.WriteLine($"Total de Transacciones: {stats.TotalTransactions}");
        Console.WriteLine($"  ✅ Exitosas: {stats.SuccessfulTransactions}");
        Console.WriteLine($"  ⏳ Pendientes: {stats.PendingTransactions}");
        Console.WriteLine($"  ❌ Canceladas: {stats.CancelledTransactions}");
        Console.WriteLine($"  ⚠️ Fallidas: {stats.FailedTransactions}");
        Console.WriteLine();
        Console.WriteLine($"Monto Total: ${stats.TotalAmount:F2}");
        Console.WriteLine($"Monto Promedio: ${stats.AverageAmount:F2}");
        Console.WriteLine();
    }

    /// <summary>
    /// Ejemplo 6: Monitorear transacciones pendientes
    /// </summary>
    public async Task Example6_MonitorPendingTransactions()
    {
        Console.WriteLine("=== Ejemplo 6: Monitorear Transacciones Pendientes ===\n");

        var byStatus = await _outboxService.GetTransactionsByStatusAsync();
        var pending = byStatus[TransactionStatus.Pending];

        if (pending.Count == 0)
        {
            Console.WriteLine("No hay transacciones pendientes.");
            return;
        }

        Console.WriteLine($"Transacciones pendientes: {pending.Count}\n");

        foreach (var tx in pending)
        {
            var elapsed = DateTime.UtcNow - tx.Timestamp;
            Console.WriteLine($"Factura: {tx.InvoiceNumber}");
            Console.WriteLine($"  Monto: ${tx.Amount}");
            Console.WriteLine($"  Tiempo transcurrido: {elapsed.TotalSeconds:F0} segundos");
            
            // Alertar si lleva mucho tiempo
            if (elapsed.TotalSeconds > 120)
            {
                Console.WriteLine("  ⚠️ ALERTA: Lleva más de 2 minutos pendiente");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Ejemplo 7: Buscar transacciones por factura
    /// </summary>
    public async Task Example7_SearchByInvoice()
    {
        Console.WriteLine("=== Ejemplo 7: Buscar por Factura ===\n");

        string invoiceNumber = "FB-12345-12345678";

        var transactions = await _outboxService.ReadTransactionsByInvoiceAsync(invoiceNumber);

        Console.WriteLine($"Búsqueda de factura: {invoiceNumber}");
        Console.WriteLine($"Archivos encontrados: {transactions.Count}\n");

        if (transactions.Count == 0)
        {
            Console.WriteLine("No se encontraron transacciones.");
            return;
        }

        foreach (var tx in transactions)
        {
            Console.WriteLine($"[{tx.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {tx.Status}");
            Console.WriteLine($"  ID: {tx.TransactionId}");
            Console.WriteLine($"  Monto: ${tx.Amount}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Ejecutar todos los ejemplos
    /// </summary>
    public async Task RunAllExamples()
    {
        await Example1_CheckTransactionStatus();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example2_ViewTransactionHistory();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example3_DetailedAnalysis();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example4_ViewTransactionsByStatus();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example5_DailyStatistics();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example6_MonitorPendingTransactions();
        Console.WriteLine("\n" + new string('=', 60) + "\n");

        await Example7_SearchByInvoice();
    }
}
