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
/// Servicio para manejar archivos de transacciones en INBOX/OUTBOX
/// </summary>
public class TransactionFileService
{
    private readonly ConfigurationService _configService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TransactionFileService(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Lee un archivo de transacción desde INBOX
    /// </summary>
    public async Task<TransactionFile?> ReadTransactionFromInboxAsync(string filename)
    {
        try
        {
            var config = _configService.GetConfig();
            var filePath = Path.Combine(config.Folders.Inbox, filename);

            if (!File.Exists(filePath))
            {
                Log.Warning("Transaction file not found: {Path}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var transaction = JsonSerializer.Deserialize<TransactionFile>(json);

            if (transaction != null)
            {
                Log.Information("Transaction read from INBOX: {TransactionId} {InvoiceNumber}", 
                    transaction.TransactionId, 
                    transaction.Detail?.InvoiceNumber);
            }

            return transaction;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading transaction from INBOX: {Filename}", filename);
            return null;
        }
    }

    /// <summary>
    /// Escribe una transacción a OUTBOX con estado de resultado
    /// </summary>
    public async Task<bool> WriteTransactionToOutboxAsync(TransactionFile transaction)
    {
        try
        {
            var config = _configService.GetConfig();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var invoiceNum = transaction.Detail?.InvoiceNumber ?? "unknown";
            var status = transaction.Status.ToString().ToLower();
            
            // Formato: TXID_Invoice_Status_Timestamp.json
            var filename = $"{transaction.ExternalId}_{invoiceNum}_{status}_{timestamp}.json";
            var filePath = Path.Combine(config.Folders.Outbox, filename);

            // Actualizar timestamp de salida
            transaction.Timestamp = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(transaction, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            Log.Information(
                "Transaction written to OUTBOX: {TransactionId} {InvoiceNumber} Status={Status}",
                transaction.TransactionId,
                transaction.Detail?.InvoiceNumber,
                transaction.Status);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing transaction to OUTBOX: {TransactionId}", transaction.TransactionId);
            return false;
        }
    }

    /// <summary>
    /// Archiva una transacción completada
    /// </summary>
    public async Task<bool> ArchiveTransactionAsync(TransactionFile transaction, string originFilename)
    {
        try
        {
            var config = _configService.GetConfig();
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            var archiveSubfolder = Path.Combine(config.Folders.Archive, "completed", timestamp);
            
            Directory.CreateDirectory(archiveSubfolder);

            var invoiceNum = transaction.Detail?.InvoiceNumber ?? "unknown";
            var status = transaction.Status.ToString().ToLower();
            var filename = $"{transaction.ExternalId}_{invoiceNum}_{status}.json";
            var archivePath = Path.Combine(archiveSubfolder, filename);

            var json = JsonSerializer.Serialize(transaction, JsonOptions);
            await File.WriteAllTextAsync(archivePath, json);

            Log.Information(
                "Transaction archived: {TransactionId} {InvoiceNumber} → {ArchivePath}",
                transaction.TransactionId,
                transaction.Detail?.InvoiceNumber,
                archivePath);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error archiving transaction: {TransactionId}", transaction.TransactionId);
            return false;
        }
    }

    /// <summary>
    /// Crea un archivo de transacción para enviar a Clover
    /// </summary>
    public TransactionFile CreateTransactionFile(
        string invoiceNumber,
        string externalId,
        decimal totalAmount,
        System.Collections.Generic.List<LineItem> items)
    {
        var transaction = new TransactionFile
        {
            TransactionId = Guid.NewGuid().ToString(),
            ExternalId = externalId,
            Timestamp = DateTime.UtcNow,
            Status = TransactionStatus.Pending,
            Type = "SALE",
            Detail = new TransactionDetail
            {
                InvoiceNumber = invoiceNumber,
                Items = items,
                Subtotal = items.Sum(i => i.GetSubtotal()),
                Tax = 0,
                Discount = 0,
                Total = totalAmount,
                Notes = "Transacción de venta"
            }
        };

        Log.Information(
            "Transaction file created: {TransactionId} Invoice={InvoiceNumber} Amount={Amount}",
            transaction.TransactionId,
            invoiceNumber,
            totalAmount);

        return transaction;
    }

    /// <summary>
    /// Actualiza estado de una transacción
    /// </summary>
    public void UpdateTransactionStatus(
        TransactionFile transaction,
        TransactionStatus newStatus,
        string? message = null,
        PaymentFileInfo? paymentInfo = null)
    {
        var oldStatus = transaction.Status;
        transaction.Status = newStatus;
        transaction.Message = message;

        if (paymentInfo != null)
        {
            transaction.PaymentInfo = paymentInfo;
        }

        Log.Information(
            "Transaction status updated: {TransactionId} {OldStatus} → {NewStatus}",
            transaction.TransactionId,
            oldStatus,
            newStatus);
    }

    /// <summary>
    /// Procesa un resultado de pago de Clover
    /// </summary>
    public void ProcessPaymentResult(TransactionFile transaction, TransactionResponse response)
    {
        if (response.Success)
        {
            UpdateTransactionStatus(
                transaction,
                TransactionStatus.Completed,
                response.Message ?? "Pago completado exitosamente");

            if (response.Payment != null)
            {
                transaction.PaymentInfo = new PaymentFileInfo
                {
                    CloverPaymentId = response.Payment.Id,
                    CloverOrderId = response.Payment.Order?.Id,
                    TotalAmount = response.Payment.Amount / 100m, // Convertir de centavos
                    Tip = response.Payment.TipAmount.HasValue ? response.Payment.TipAmount.Value / 100m : null
                };

                if (response.Payment.CardTransaction != null)
                {
                    transaction.PaymentInfo.CardLast4 = response.Payment.CardTransaction.Last4;
                    transaction.PaymentInfo.CardBrand = response.Payment.CardTransaction.CardType;
                    transaction.PaymentInfo.AuthCode = response.Payment.CardTransaction.AuthCode;
                }
            }
        }
        else
        {
            UpdateTransactionStatus(
                transaction,
                TransactionStatus.Failed,
                response.Reason ?? "Pago rechazado",
                null);

            transaction.Result = "DECLINED";
            transaction.ErrorCode = response.Reason;
        }
    }

    /// <summary>
    /// Limpia la carpeta INBOX eliminando archivos procesados
    /// </summary>
    public async Task<bool> CleanupInboxAsync()
    {
        try
        {
            var config = _configService.GetConfig();
            var inboxPath = config.Folders.Inbox;

            if (!Directory.Exists(inboxPath))
            {
                return true; // No hay carpeta, nada que limpiar
            }

            var files = Directory.GetFiles(inboxPath);
            foreach (var file in files)
            {
                File.Delete(file);
            }

            Log.Information("INBOX cleaned: {FileCount} files removed", files.Length);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning INBOX");
            return false;
        }
    }

    /// <summary>
    /// Lista los archivos disponibles en INBOX para procesar
    /// </summary>
    public List<string> ListInboxFiles()
    {
        try
        {
            var config = _configService.GetConfig();
            var inboxPath = config.Folders.Inbox;

            if (!Directory.Exists(inboxPath))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(inboxPath, "*.json")
                .Select(Path.GetFileName)
                .ToList();

            Log.Information("INBOX files listed: {FileCount} files found", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error listing INBOX files");
            return new List<string>();
        }
    }}