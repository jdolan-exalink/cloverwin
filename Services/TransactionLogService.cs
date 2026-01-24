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
/// Servicio para mantener un registro histórico de todas las transacciones
/// </summary>
public class TransactionLogService
{
    private readonly ConfigurationService _configService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TransactionLogService(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Registra una transacción en el log histórico
    /// </summary>
    public async Task<bool> LogTransactionAsync(TransactionFile transaction)
    {
        try
        {
            var config = _configService.GetConfig();
            var logDir = Path.Combine(config.Folders.Archive, "TransactionLog");
            Directory.CreateDirectory(logDir);

            var today = DateTime.Now.ToString("yyyyMMdd");
            var logFile = Path.Combine(logDir, $"transactions_{today}.jsonl");

            // Crear registro de transacción con información completa
            var logEntry = new TransactionLogRecord
            {
                TransactionId = transaction.TransactionId,
                InvoiceNumber = transaction.InvoiceNumber,
                ExternalId = transaction.ExternalId,
                Amount = transaction.Amount,
                Status = transaction.Status.ToString(),
                Type = transaction.Type,
                
                // Tiempos
                ReceivedTime = transaction.Timestamp,
                ProcessStartTime = transaction.ProcessStartTime,
                SentToTerminalTime = transaction.SentToTerminalTime,
                ProcessEndTime = transaction.ProcessEndTime,
                
                // Duración total
                TotalProcessingSeconds = transaction.ProcessEndTime.HasValue && transaction.ProcessStartTime.HasValue
                    ? (transaction.ProcessEndTime.Value - transaction.ProcessStartTime.Value).TotalSeconds
                    : null,
                
                // Resultado
                PaymentInfo = transaction.PaymentInfo,
                ErrorMessage = transaction.ErrorMessage,
                ErrorCode = transaction.ErrorCode,
                
                // Log de eventos
                TransactionLog = transaction.TransactionLog,
                
                // Metadata
                CustomerName = transaction.CustomerName,
                Notes = transaction.Notes
            };

            // Escribir como JSON Lines (una línea por transacción)
            var json = JsonSerializer.Serialize(logEntry, JsonOptions);
            await File.AppendAllTextAsync(logFile, json + Environment.NewLine);

            Log.Information("Transaction logged: Invoice={Invoice} Status={Status} LogFile={LogFile}",
                transaction.InvoiceNumber, transaction.Status, logFile);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error logging transaction: {TransactionId}", transaction.TransactionId);
            return false;
        }
    }

    /// <summary>
    /// Obtiene el resumen de transacciones de hoy
    /// </summary>
    public async Task<TransactionSummary> GetTodaysSummaryAsync()
    {
        try
        {
            var config = _configService.GetConfig();
            var logDir = Path.Combine(config.Folders.Archive, "TransactionLog");
            var today = DateTime.Now.ToString("yyyyMMdd");
            var logFile = Path.Combine(logDir, $"transactions_{today}.jsonl");

            if (!File.Exists(logFile))
            {
                return new TransactionSummary { Date = today };
            }

            var lines = await File.ReadAllLinesAsync(logFile);
            var transactions = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<TransactionLogRecord>(l))
                .Where(t => t != null)
                .ToList();

            var summary = new TransactionSummary
            {
                Date = today,
                TotalTransactions = transactions.Count,
                SuccessfulCount = transactions.Count(t => t!.Status == "Successful"),
                FailedCount = transactions.Count(t => t!.Status == "Failed"),
                CancelledCount = transactions.Count(t => t!.Status == "Cancelled"),
                TimeoutCount = transactions.Count(t => t!.Status == "Timeout"),
                InsufficientFundsCount = transactions.Count(t => t!.Status == "InsufficientFunds"),
                
                TotalAmount = transactions
                    .Where(t => t!.Status == "Successful")
                    .Sum(t => t!.Amount),
                
                AverageProcessingSeconds = transactions
                    .Where(t => t!.TotalProcessingSeconds.HasValue)
                    .Select(t => t!.TotalProcessingSeconds!.Value)
                    .DefaultIfEmpty(0)
                    .Average()
            };

            return summary;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting today's summary");
            return new TransactionSummary { Date = DateTime.Now.ToString("yyyyMMdd") };
        }
    }

    /// <summary>
    /// Busca transacciones por número de factura
    /// </summary>
    public async Task<List<TransactionLogRecord>> SearchByInvoiceNumberAsync(string invoiceNumber, int daysBack = 7)
    {
        var results = new List<TransactionLogRecord>();
        
        try
        {
            var config = _configService.GetConfig();
            var logDir = Path.Combine(config.Folders.Archive, "TransactionLog");

            if (!Directory.Exists(logDir))
            {
                return results;
            }

            // Buscar en los últimos N días
            for (int i = 0; i < daysBack; i++)
            {
                var date = DateTime.Now.AddDays(-i).ToString("yyyyMMdd");
                var logFile = Path.Combine(logDir, $"transactions_{date}.jsonl");

                if (File.Exists(logFile))
                {
                    var lines = await File.ReadAllLinesAsync(logFile);
                    var transactions = lines
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => JsonSerializer.Deserialize<TransactionLogRecord>(l))
                        .Where(t => t != null && t.InvoiceNumber == invoiceNumber)
                        .ToList();

                    results.AddRange(transactions!);
                }
            }

            Log.Information("Search completed: Invoice={Invoice} Found={Count}", invoiceNumber, results.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching transactions by invoice number: {InvoiceNumber}", invoiceNumber);
        }

        return results;
    }
}

/// <summary>
/// Registro de transacción para el log histórico
/// </summary>
public class TransactionLogRecord
{
    public string TransactionId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    
    // Tiempos
    public DateTime ReceivedTime { get; set; }
    public DateTime? ProcessStartTime { get; set; }
    public DateTime? SentToTerminalTime { get; set; }
    public DateTime? ProcessEndTime { get; set; }
    public double? TotalProcessingSeconds { get; set; }
    
    // Resultado
    public PaymentFileInfo? PaymentInfo { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    
    // Log de eventos
    public List<TransactionLogEntry> TransactionLog { get; set; } = new();
    
    // Metadata
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Resumen de transacciones
/// </summary>
public class TransactionSummary
{
    public string Date { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TimeoutCount { get; set; }
    public int InsufficientFundsCount { get; set; }
    public decimal TotalAmount { get; set; }
    public double AverageProcessingSeconds { get; set; }
}
