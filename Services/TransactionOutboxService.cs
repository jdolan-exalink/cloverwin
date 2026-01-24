using CloverBridge.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloverBridge.Services;

/// <summary>
/// Servicio especializado para leer y analizar archivos de OUTBOX
/// </summary>
public class TransactionOutboxService
{
    private readonly ConfigurationService _configService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TransactionOutboxService(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Lee todos los archivos de transacciones desde OUTBOX
    /// </summary>
    public async Task<List<TransactionFile>> ReadAllTransactionsFromOutboxAsync()
    {
        var transactions = new List<TransactionFile>();

        try
        {
            var config = _configService.GetConfig();
            var outboxPath = config.Folders.Outbox;

            if (!Directory.Exists(outboxPath))
            {
                Log.Warning("OUTBOX directory does not exist: {Path}", outboxPath);
                return transactions;
            }

            var files = Directory.GetFiles(outboxPath, "*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            Log.Information("Reading {FileCount} transaction files from OUTBOX", files.Count);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var transaction = JsonSerializer.Deserialize<TransactionFile>(json, JsonOptions);
                    
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error reading transaction file: {File}", Path.GetFileName(file));
                }
            }

            Log.Information("Successfully read {Count} transactions from OUTBOX", transactions.Count);
            return transactions;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading transactions from OUTBOX");
            return transactions;
        }
    }

    /// <summary>
    /// Lee todos los archivos de una factura específica desde OUTBOX
    /// </summary>
    public async Task<List<TransactionFile>> ReadTransactionsByInvoiceAsync(string invoiceNumber)
    {
        var transactions = new List<TransactionFile>();

        try
        {
            var config = _configService.GetConfig();
            var outboxPath = config.Folders.Outbox;

            if (!Directory.Exists(outboxPath))
            {
                Log.Warning("OUTBOX directory does not exist: {Path}", outboxPath);
                return transactions;
            }

            // Buscar archivos que coincidan con el invoice number
            var files = Directory.GetFiles(outboxPath, $"{invoiceNumber}_*.json")
                .OrderBy(f => File.GetLastWriteTime(f))
                .ToList();

            Log.Information("Found {FileCount} files for invoice: {InvoiceNumber}", files.Count, invoiceNumber);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var transaction = JsonSerializer.Deserialize<TransactionFile>(json, JsonOptions);
                    
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error reading transaction file: {File}", Path.GetFileName(file));
                }
            }

            Log.Information("Successfully read {Count} transactions for invoice {InvoiceNumber}", 
                transactions.Count, invoiceNumber);
            
            return transactions;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading transactions by invoice: {InvoiceNumber}", invoiceNumber);
            return transactions;
        }
    }

    /// <summary>
    /// Obtiene las transacciones agrupadas por estado
    /// </summary>
    public async Task<Dictionary<TransactionStatus, List<TransactionFile>>> GetTransactionsByStatusAsync()
    {
        var result = new Dictionary<TransactionStatus, List<TransactionFile>>();
        
        // Inicializar diccionario con todos los estados
        foreach (TransactionStatus status in Enum.GetValues(typeof(TransactionStatus)))
        {
            result[status] = new List<TransactionFile>();
        }

        try
        {
            var allTransactions = await ReadAllTransactionsFromOutboxAsync();

            foreach (var transaction in allTransactions)
            {
                result[transaction.Status].Add(transaction);
            }

            Log.Information("Transactions grouped by status: " +
                "Pending={Pending}, Processing={Processing}, Successful={Successful}, " +
                "Cancelled={Cancelled}, Timeout={Timeout}, Failed={Failed}",
                result[TransactionStatus.Pending].Count,
                result[TransactionStatus.Processing].Count,
                result[TransactionStatus.Successful].Count,
                result[TransactionStatus.Cancelled].Count,
                result[TransactionStatus.Timeout].Count,
                result[TransactionStatus.Failed].Count);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error grouping transactions by status");
            return result;
        }
    }

    /// <summary>
    /// Obtiene el historial completo de una transacción (todos los estados por los que pasó)
    /// </summary>
    public async Task<List<TransactionFile>> GetTransactionHistoryAsync(string invoiceNumber)
    {
        try
        {
            var transactions = await ReadTransactionsByInvoiceAsync(invoiceNumber);
            
            // Ordenar por timestamp para ver la progresión
            var history = transactions.OrderBy(t => t.Timestamp).ToList();

            Log.Information("Transaction history for {InvoiceNumber}: {Count} state changes", 
                invoiceNumber, history.Count);

            return history;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting transaction history for: {InvoiceNumber}", invoiceNumber);
            return new List<TransactionFile>();
        }
    }

    /// <summary>
    /// Verifica si una transacción está pendiente
    /// </summary>
    public async Task<bool> IsTransactionPendingAsync(string invoiceNumber)
    {
        try
        {
            var transactions = await ReadTransactionsByInvoiceAsync(invoiceNumber);
            var latest = transactions.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            return latest?.Status == TransactionStatus.Pending;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking if transaction is pending: {InvoiceNumber}", invoiceNumber);
            return false;
        }
    }

    /// <summary>
    /// Verifica si una transacción está completada (exitosa, cancelada o con error)
    /// </summary>
    public async Task<bool> IsTransactionCompletedAsync(string invoiceNumber)
    {
        try
        {
            var transactions = await ReadTransactionsByInvoiceAsync(invoiceNumber);
            var latest = transactions.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            
            if (latest == null)
                return false;

            return latest.Status == TransactionStatus.Successful ||
                   latest.Status == TransactionStatus.Cancelled ||
                   latest.Status == TransactionStatus.Failed ||
                   latest.Status == TransactionStatus.Timeout ||
                   latest.Status == TransactionStatus.InsufficientFunds;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking if transaction is completed: {InvoiceNumber}", invoiceNumber);
            return false;
        }
    }

    /// <summary>
    /// Obtiene el estado actual de una transacción
    /// </summary>
    public async Task<TransactionStatus?> GetTransactionStatusAsync(string invoiceNumber)
    {
        try
        {
            var transactions = await ReadTransactionsByInvoiceAsync(invoiceNumber);
            var latest = transactions.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            return latest?.Status;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting transaction status: {InvoiceNumber}", invoiceNumber);
            return null;
        }
    }

    /// <summary>
    /// Obtiene estadísticas de transacciones del día
    /// </summary>
    public async Task<TransactionStats> GetDailyStatsAsync()
    {
        var stats = new TransactionStats();

        try
        {
            var allTransactions = await ReadAllTransactionsFromOutboxAsync();
            var today = DateTime.Today;

            var todayTransactions = allTransactions
                .Where(t => t.Timestamp.Date == today)
                .ToList();

            stats.TotalTransactions = todayTransactions.Count;
            stats.SuccessfulTransactions = todayTransactions.Count(t => t.Status == TransactionStatus.Successful);
            stats.PendingTransactions = todayTransactions.Count(t => t.Status == TransactionStatus.Pending);
            stats.CancelledTransactions = todayTransactions.Count(t => t.Status == TransactionStatus.Cancelled);
            stats.FailedTransactions = todayTransactions.Count(t => t.Status == TransactionStatus.Failed || 
                                                                     t.Status == TransactionStatus.Timeout ||
                                                                     t.Status == TransactionStatus.InsufficientFunds);

            stats.TotalAmount = todayTransactions
                .Where(t => t.Status == TransactionStatus.Successful)
                .Sum(t => t.Amount);

            stats.AverageAmount = stats.SuccessfulTransactions > 0 
                ? stats.TotalAmount / stats.SuccessfulTransactions 
                : 0;

            Log.Information("Daily stats: Total={Total}, Successful={Successful}, Pending={Pending}, " +
                "Cancelled={Cancelled}, Failed={Failed}, TotalAmount={TotalAmount}",
                stats.TotalTransactions, stats.SuccessfulTransactions, stats.PendingTransactions,
                stats.CancelledTransactions, stats.FailedTransactions, stats.TotalAmount);

            return stats;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting daily stats");
            return stats;
        }
    }

    /// <summary>
    /// Analiza una transacción específica y muestra sus detalles
    /// </summary>
    public async Task<TransactionAnalysis> AnalyzeTransactionAsync(string invoiceNumber)
    {
        var analysis = new TransactionAnalysis
        {
            InvoiceNumber = invoiceNumber
        };

        try
        {
            var history = await GetTransactionHistoryAsync(invoiceNumber);
            
            if (history.Count == 0)
            {
                analysis.Status = "NOT_FOUND";
                analysis.Message = $"No se encontraron archivos para la factura {invoiceNumber}";
                return analysis;
            }

            analysis.TotalStates = history.Count;
            analysis.States = history.Select(t => new StateInfo
            {
                Status = t.Status,
                Timestamp = t.Timestamp,
                Amount = t.Amount,
                ErrorMessage = t.ErrorMessage
            }).ToList();

            var latest = history.Last();
            analysis.CurrentStatus = latest.Status;
            analysis.Amount = latest.Amount;
            analysis.CustomerName = latest.CustomerName;
            analysis.Notes = latest.Notes;

            if (latest.PaymentInfo != null)
            {
                analysis.PaymentDetails = $"Card: {latest.PaymentInfo.CardBrand} ****{latest.PaymentInfo.CardLast4}, " +
                                         $"Total: ${latest.PaymentInfo.TotalAmount}";
            }

            // Calcular duración
            if (history.Count > 1)
            {
                var first = history.First();
                var last = history.Last();
                var duration = last.Timestamp - first.Timestamp;
                analysis.ProcessingDuration = $"{duration.TotalSeconds:F2} segundos";
            }

            analysis.Status = "OK";
            analysis.Message = $"Transacción analizada correctamente. Estados: {history.Count}";

            Log.Information("Transaction analysis completed for {InvoiceNumber}: {States} states found, current status: {Status}",
                invoiceNumber, history.Count, latest.Status);

            return analysis;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error analyzing transaction: {InvoiceNumber}", invoiceNumber);
            analysis.Status = "ERROR";
            analysis.Message = ex.Message;
            return analysis;
        }
    }
}

/// <summary>
/// Estadísticas de transacciones
/// </summary>
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

/// <summary>
/// Análisis completo de una transacción
/// </summary>
public class TransactionAnalysis
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int TotalStates { get; set; }
    public TransactionStatus CurrentStatus { get; set; }
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
    public string? PaymentDetails { get; set; }
    public string? ProcessingDuration { get; set; }
    public List<StateInfo> States { get; set; } = new();
}

/// <summary>
/// Información de un estado de transacción
/// </summary>
public class StateInfo
{
    public TransactionStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Amount { get; set; }
    public string? ErrorMessage { get; set; }
}
