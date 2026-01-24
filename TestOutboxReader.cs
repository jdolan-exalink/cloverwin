using CloverBridge.Models;
using CloverBridge.Services;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CloverBridge;

/// <summary>
/// Programa de prueba para leer y analizar transacciones desde OUTBOX
/// </summary>
public class TestOutboxReader
{
    public static async Task TestOutboxAsync()
    {
        // Configurar Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/outbox-test-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("=== OUTBOX Transaction Reader Test ===");
            
            // Inicializar servicios
            var configService = new ConfigurationService();
            var outboxService = new TransactionOutboxService(configService);

            // Verificar configuración
            var config = configService.GetConfig();
            Log.Information("OUTBOX Path: {Path}", config.Folders.Outbox);

            // 1. Leer todas las transacciones
            Console.WriteLine("\n=== Reading all transactions from OUTBOX ===");
            var allTransactions = await outboxService.ReadAllTransactionsFromOutboxAsync();
            Console.WriteLine($"Total transactions found: {allTransactions.Count}");

            // 2. Agrupar por estado
            Console.WriteLine("\n=== Transactions by Status ===");
            var byStatus = await outboxService.GetTransactionsByStatusAsync();
            foreach (var statusGroup in byStatus)
            {
                Console.WriteLine($"{statusGroup.Key}: {statusGroup.Value.Count} transactions");
                foreach (var tx in statusGroup.Value.Take(3)) // Mostrar solo los primeros 3
                {
                    Console.WriteLine($"  - {tx.InvoiceNumber} | ${tx.Amount} | {tx.Timestamp:yyyy-MM-dd HH:mm:ss}");
                }
            }

            // 3. Estadísticas del día
            Console.WriteLine("\n=== Daily Statistics ===");
            var stats = await outboxService.GetDailyStatsAsync();
            Console.WriteLine($"Total Transactions: {stats.TotalTransactions}");
            Console.WriteLine($"Successful: {stats.SuccessfulTransactions}");
            Console.WriteLine($"Pending: {stats.PendingTransactions}");
            Console.WriteLine($"Cancelled: {stats.CancelledTransactions}");
            Console.WriteLine($"Failed: {stats.FailedTransactions}");
            Console.WriteLine($"Total Amount: ${stats.TotalAmount:F2}");
            Console.WriteLine($"Average Amount: ${stats.AverageAmount:F2}");

            // 4. Analizar una transacción específica si hay archivos
            if (allTransactions.Count > 0)
            {
                // Buscar una factura para analizar
                var sampleInvoice = allTransactions.First().InvoiceNumber;
                
                Console.WriteLine($"\n=== Analyzing Transaction: {sampleInvoice} ===");
                var analysis = await outboxService.AnalyzeTransactionAsync(sampleInvoice);
                
                Console.WriteLine($"Status: {analysis.Status}");
                Console.WriteLine($"Message: {analysis.Message}");
                Console.WriteLine($"Current Status: {analysis.CurrentStatus}");
                Console.WriteLine($"Amount: ${analysis.Amount}");
                Console.WriteLine($"Customer: {analysis.CustomerName ?? "N/A"}");
                Console.WriteLine($"Duration: {analysis.ProcessingDuration ?? "N/A"}");
                Console.WriteLine($"Payment: {analysis.PaymentDetails ?? "N/A"}");
                
                Console.WriteLine($"\nState History ({analysis.States.Count} states):");
                foreach (var state in analysis.States)
                {
                    var errorMsg = state.ErrorMessage != null ? $" - {state.ErrorMessage}" : "";
                    Console.WriteLine($"  [{state.Timestamp:HH:mm:ss}] {state.Status}{errorMsg}");
                }
            }

            // 5. Prueba con la factura de ejemplo
            Console.WriteLine("\n=== Testing with Sample Invoice: FB-12345-12345678 ===");
            var sampleAnalysis = await outboxService.AnalyzeTransactionAsync("FB-12345-12345678");
            
            Console.WriteLine($"Status: {sampleAnalysis.Status}");
            Console.WriteLine($"Message: {sampleAnalysis.Message}");
            
            if (sampleAnalysis.Status == "OK")
            {
                Console.WriteLine($"Current Status: {sampleAnalysis.CurrentStatus}");
                Console.WriteLine($"Amount: ${sampleAnalysis.Amount}");
                Console.WriteLine($"Total States: {sampleAnalysis.TotalStates}");
                Console.WriteLine($"Duration: {sampleAnalysis.ProcessingDuration}");
                
                Console.WriteLine("\nState Progression:");
                foreach (var state in sampleAnalysis.States)
                {
                    Console.WriteLine($"  {state.Timestamp:yyyy-MM-dd HH:mm:ss.fff} -> {state.Status}");
                }
            }

            // 6. Verificar estados específicos
            Console.WriteLine("\n=== Checking Transaction States ===");
            var isPending = await outboxService.IsTransactionPendingAsync("FB-12345-12345678");
            var isCompleted = await outboxService.IsTransactionCompletedAsync("FB-12345-12345678");
            var currentStatus = await outboxService.GetTransactionStatusAsync("FB-12345-12345678");
            
            Console.WriteLine($"Is Pending: {isPending}");
            Console.WriteLine($"Is Completed: {isCompleted}");
            Console.WriteLine($"Current Status: {currentStatus}");

            Log.Information("Test completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during test");
            Console.WriteLine($"\nError: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
