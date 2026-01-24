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
                Log.Information("Transaction read from INBOX: {InvoiceNumber} Amount={Amount}", 
                    transaction.InvoiceNumber, 
                    transaction.Amount);
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
            var invoiceNum = transaction.InvoiceNumber ?? "unknown";
            var status = transaction.Status.ToString().ToLower();
            
            // Formato: Invoice_Status_Timestamp.json
            var filename = $"{invoiceNum}_{status}_{timestamp}.json";
            var filePath = Path.Combine(config.Folders.Outbox, filename);

            // Actualizar timestamp de salida
            transaction.Timestamp = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(transaction, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            Log.Information(
                "Transaction written to OUTBOX: {InvoiceNumber} Status={Status} Amount={Amount}",
                transaction.InvoiceNumber,
                transaction.Status,
                transaction.Amount);

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
            var archiveSubfolder = Path.Combine(config.Folders.Archive, timestamp);
            
            Directory.CreateDirectory(archiveSubfolder);

            var invoiceNum = transaction.InvoiceNumber ?? "unknown";
            var status = transaction.Status.ToString().ToLower();
            var filename = $"{invoiceNum}_{status}_{DateTime.Now:HHmmss}.json";
            var archivePath = Path.Combine(archiveSubfolder, filename);

            var json = JsonSerializer.Serialize(transaction, JsonOptions);
            await File.WriteAllTextAsync(archivePath, json);

            Log.Information(
                "Transaction archived: {InvoiceNumber} Status={Status} → {ArchivePath}",
                transaction.InvoiceNumber,
                transaction.Status,
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
    /// Lee la última transacción desde OUTBOX por invoice number
    /// </summary>
    public async Task<TransactionFile?> ReadLatestTransactionFromOutboxAsync(string invoiceNumber)
    {
        try
        {
            var config = _configService.GetConfig();
            var outboxPath = config.Folders.Outbox;
            
            if (!Directory.Exists(outboxPath))
            {
                Log.Warning("OUTBOX directory does not exist: {Path}", outboxPath);
                return null;
            }

            // Buscar archivos que coincidan con el invoice number
            var matchingFiles = Directory.GetFiles(outboxPath, $"{invoiceNumber}_*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();

            if (matchingFiles == null)
            {
                Log.Information("No transaction found in OUTBOX for invoice: {InvoiceNumber}", invoiceNumber);
                return null;
            }

            var json = await File.ReadAllTextAsync(matchingFiles);
            var transaction = JsonSerializer.Deserialize<TransactionFile>(json);

            Log.Information("Transaction read from OUTBOX: {InvoiceNumber} Status={Status}", 
                transaction?.InvoiceNumber, transaction?.Status);

            return transaction;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading transaction from OUTBOX: {InvoiceNumber}", invoiceNumber);
            return null;
        }
    }

    /// <summary>
    /// Crea un archivo de transacción para enviar a Clover
    /// </summary>
    public TransactionFile CreateTransactionFile(
        string invoiceNumber,
        string externalId,
        decimal totalAmount,
        string? customerName = null,
        string? notes = null,
        decimal? tax = null)
    {
        var transaction = new TransactionFile
        {
            TransactionId = Guid.NewGuid().ToString(),
            ExternalId = externalId,
            Timestamp = DateTime.UtcNow,
            Status = TransactionStatus.Pending,
            Type = "SALE",
            InvoiceNumber = invoiceNumber,
            Amount = totalAmount,
            Tax = tax,
            CustomerName = customerName,
            Notes = notes
        };

        Log.Information(
            "Transaction file created: {InvoiceNumber} Amount={Amount}",
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
        string? errorMessage = null,
        PaymentFileInfo? paymentInfo = null)
    {
        var oldStatus = transaction.Status;
        transaction.Status = newStatus;
        transaction.ErrorMessage = errorMessage;

        if (paymentInfo != null)
        {
            transaction.PaymentInfo = paymentInfo;
        }

        Log.Information(
            "Transaction status updated: {InvoiceNumber} {OldStatus} → {NewStatus}",
            transaction.InvoiceNumber,
            oldStatus,
            newStatus);
    }

    /// <summary>
    /// Procesa un resultado de pago de Clover y determina el estado correcto
    /// Mapeo según documento de integración Clover:
    /// - resultado.estado = "aprobado" si response.result == "SUCCESS"
    /// - resultado.id_transaccion = response.id
    /// - resultado.numero_de_cupon = response.cardTransaction.authCode
    /// - resultado.tarjeta = response.cardTransaction.cardType + " " + last4
    /// - resultado.nro_de_factura = response.externalPaymentId
    /// </summary>
    public void ProcessPaymentResult(TransactionFile transaction, TransactionResponse response)
    {
        Log.Information("ProcessPaymentResult: Processing response - Provider={Provider}, Success={Success}, PaymentId={PaymentId}",
            transaction.Provider, response.Success, response.Payment?.Id ?? "N/A");

        // Caso Mercado Pago (QRMP)
        if (transaction.Provider == "QRMP")
        {
            ProcessMpResult(transaction, response);
            return;
        }

        // Caso Clover (Default)
        ProcessCloverResult(transaction, response);
    }

    private void ProcessMpResult(TransactionFile transaction, TransactionResponse response)
    {
        TransactionStatus newStatus = response.Success ? TransactionStatus.Successful : TransactionStatus.Failed;
        
        if (response.Success && response.Payment != null)
        {
            transaction.PaymentInfo ??= new PaymentFileInfo();
            transaction.PaymentInfo.TotalAmount = transaction.Amount;
            transaction.PaymentInfo.Currency = transaction.Currency;
            
            // Llenar detalles de MP
            transaction.PaymentInfo.Mp = new MpPaymentDetail
            {
                PaymentId = response.Payment.Id,
                Status = response.Success ? "approved" : "failed",
                StatusDetail = response.Reason,
                DateApproved = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff-03:00")
            };
            
            transaction.TransactionId = response.Payment.Id; // Usar ID de MP como transactionId
            transaction.AddLogEntry("MP_PAID", "Pago aprobado en Mercado Pago", $"ID: {response.Payment.Id}");
        }
        else
        {
            transaction.ErrorMessage = response.Message ?? "Pago rechazado o fallido en Mercado Pago";
            transaction.AddLogEntry("MP_FAILED", "Pago no aprobado", response.Reason);
        }

        UpdateTransactionStatus(transaction, newStatus, transaction.ErrorMessage);
    }

    private void ProcessCloverResult(TransactionFile transaction, TransactionResponse response)
    {
        Log.Information("ProcessCloverResult: Processing Clover response");
        TransactionStatus newStatus;
        
        if (response.Success)
        {
            // Pago exitoso - ESTADO = "aprobado"
            newStatus = TransactionStatus.Successful;
            
            // Crear o actualizar PaymentInfo
            transaction.PaymentInfo ??= new PaymentFileInfo();
            
            if (response.Payment != null)
            {
                // ID_TRANSACCION = response.id (vital para refunds)
                transaction.PaymentInfo.CloverPaymentId = response.Payment.Id;
                
                // Order ID si está disponible
                transaction.PaymentInfo.CloverOrderId = response.Payment.Order?.Id;
                
                // Montos (convertir de centavos a dólares/pesos)
                transaction.PaymentInfo.TotalAmount = response.Payment.Amount / 100m;
                transaction.PaymentInfo.Tip = response.Payment.TipAmount.HasValue 
                    ? response.Payment.TipAmount.Value / 100m 
                    : null;
                
                transaction.PaymentInfo.ProcessingStartTime = transaction.ProcessStartTime ?? DateTime.UtcNow;

                // TARJETA y NUMERO_DE_CUPON desde cardTransaction
                if (response.Payment.CardTransaction != null)
                {
                    // NUMERO_DE_CUPON = authCode (código de autorización)
                    transaction.PaymentInfo.AuthCode = response.Payment.CardTransaction.AuthCode;
                    
                    // TARJETA = cardType + " " + last4 (ej: "VISA 4242")
                    transaction.PaymentInfo.CardLast4 = response.Payment.CardTransaction.Last4;
                    transaction.PaymentInfo.CardBrand = response.Payment.CardTransaction.CardType;
                    
                    // Moneda (ARS, USD, etc.)
                    transaction.PaymentInfo.Currency = response.Payment.CardTransaction.Currency?.ToUpper();
                    
                    Log.Information("ProcessPaymentResult: Card info - Brand={Brand}, Last4={Last4}, AuthCode={AuthCode}, Currency={Currency}",
                        transaction.PaymentInfo.CardBrand,
                        transaction.PaymentInfo.CardLast4,
                        transaction.PaymentInfo.AuthCode,
                        transaction.PaymentInfo.Currency);
                }
                
                // MÉTODO DE PAGO desde transactionInfo (ej: "MERCADO PAGO Transferencia")
                if (response.Payment.TransactionInfo != null)
                {
                    transaction.PaymentInfo.PaymentMethod = response.Payment.TransactionInfo.CardTypeLabel;
                    transaction.PaymentInfo.EntryType = response.Payment.TransactionInfo.EntryType;
                    
                    Log.Information("ProcessPaymentResult: PaymentMethod={Method}, EntryType={Entry}",
                        transaction.PaymentInfo.PaymentMethod,
                        transaction.PaymentInfo.EntryType);
                }
                
                // NOTA del pago (contiene info adicional como ID QR, Lote, Cupón, Billetera)
                if (!string.IsNullOrEmpty(response.Payment.Note))
                {
                    transaction.PaymentInfo.PaymentNote = response.Payment.Note;
                    Log.Information("ProcessPaymentResult: PaymentNote={Note}", response.Payment.Note);
                }
                
                // TENDER (tipo de medio de pago, ej: ar.com.fiserv.fiservqr.prod)
                if (response.Payment.Tender != null)
                {
                    transaction.PaymentInfo.TenderLabel = response.Payment.Tender.LabelKey;
                    Log.Information("ProcessPaymentResult: TenderLabel={Tender}", transaction.PaymentInfo.TenderLabel);
                }
                
                // Device, Merchant, Employee IDs
                transaction.PaymentInfo.DeviceId = response.Payment.Device?.Id;
                transaction.PaymentInfo.MerchantId = response.Payment.Merchant?.Id;
                transaction.PaymentInfo.EmployeeId = response.Payment.Employee?.Id;
                
                // Timestamp de la transacción en Clover
                if (response.Payment.CreatedTime.HasValue)
                {
                    try
                    {
                        transaction.PaymentInfo.TransactionTime = 
                            DateTimeOffset.FromUnixTimeMilliseconds(response.Payment.CreatedTime.Value).UtcDateTime;
                        Log.Information("ProcessPaymentResult: TransactionTime={Time}", transaction.PaymentInfo.TransactionTime);
                    }
                    catch { }
                }
                
                // NRO_DE_FACTURA = externalPaymentId (si se envió en el request)
                if (!string.IsNullOrEmpty(response.Payment.ExternalPaymentId))
                {
                    Log.Information("ProcessPaymentResult: ExternalPaymentId from response = {ExtId}", 
                        response.Payment.ExternalPaymentId);
                }
                
                transaction.AddLogEntry("PAYMENT_SUCCESS", "Pago exitoso", 
                    $"PaymentId: {response.Payment.Id}, AuthCode: {response.Payment.CardTransaction?.AuthCode ?? "N/A"}, Method: {transaction.PaymentInfo.PaymentMethod ?? "N/A"}");
            }
            else
            {
                // Success pero sin Payment object - usar datos del response principal
                Log.Warning("ProcessPaymentResult: Success=true but Payment is null");
                transaction.AddLogEntry("PAYMENT_SUCCESS", "Pago exitoso (sin detalles)", null);
            }
            
            // Si tenemos externalPaymentId en el response principal, usarlo
            if (!string.IsNullOrEmpty(response.ExternalPaymentId))
            {
                Log.Information("ProcessPaymentResult: Using ExternalPaymentId = {ExtId}", response.ExternalPaymentId);
            }
        }
        else
        {
            // Determinar el tipo de fallo - ESTADO = "fallo"
            var reason = response.Reason?.ToLower() ?? "";
            
            if (reason.Contains("cancel"))
            {
                newStatus = TransactionStatus.Cancelled;
                transaction.ErrorMessage = "Transacción cancelada por el usuario";
                transaction.AddLogEntry("CANCELLED", "Transacción cancelada", reason);
            }
            else if (reason.Contains("timeout"))
            {
                newStatus = TransactionStatus.Timeout;
                transaction.ErrorMessage = "Timeout - Sin respuesta del terminal";
                transaction.AddLogEntry("TIMEOUT", "Timeout alcanzado", reason);
            }
            else if (reason.Contains("insufficient") || reason.Contains("declined") || 
                     reason.Contains("denied") || reason.Contains("funds") ||
                     reason.Contains("reject"))
            {
                newStatus = TransactionStatus.InsufficientFunds;
                transaction.ErrorMessage = "Fondos insuficientes o tarjeta rechazada";
                transaction.AddLogEntry("INSUFFICIENT_FUNDS", "Fondos insuficientes", reason);
            }
            else if (string.IsNullOrEmpty(reason) || reason == "no payload received")
            {
                // Si no hay razón específica, podría ser un timeout o cancelación
                newStatus = TransactionStatus.Failed;
                transaction.ErrorMessage = "Sin respuesta del terminal";
                transaction.AddLogEntry("FAILED", "Sin respuesta del terminal", "No se recibió payload de Clover");
            }
            else
            {
                newStatus = TransactionStatus.Failed;
                transaction.ErrorMessage = response.Reason ?? "Error desconocido";
                transaction.AddLogEntry("FAILED", "Transacción fallida", reason);
            }
            
            transaction.ErrorCode = response.Reason;
            
            Log.Warning("ProcessPaymentResult: Transaction failed - Status={Status}, Reason={Reason}",
                newStatus, response.Reason);
        }

        UpdateTransactionStatus(transaction, newStatus, transaction.ErrorMessage, transaction.PaymentInfo);
        
        Log.Information("ProcessPaymentResult: Final status = {Status}", newStatus);
    }

    /// <summary>
    /// Procesa un resultado de pago de Clover directamente desde CloverMessage
    /// </summary>
    public void ProcessPaymentResult(TransactionFile transaction, CloverMessage cloverMessage)
    {
        var response = ConvertToTransactionResponse(cloverMessage);
        ProcessPaymentResult(transaction, response);
    }

    /// <summary>
    /// Convierte CloverMessage a TransactionResponse
    /// Implementa el mapeo según documento de integración Clover:
    /// - result == "SUCCESS" indica éxito
    /// - id = ID de transacción real
    /// - cardTransaction.authCode = número de cupón
    /// - cardTransaction.cardType + last4 = tarjeta
    /// - externalPaymentId = número de factura (si se envió en request)
    /// </summary>
    private TransactionResponse ConvertToTransactionResponse(CloverMessage cloverMessage)
    {
        try
        {
            // El Payload es object?, necesitamos convertirlo a JsonElement
            if (cloverMessage.Payload == null)
            {
                Log.Warning("ConvertToTransactionResponse: No payload received");
                return new TransactionResponse
                {
                    Success = false,
                    Reason = "No payload received"
                };
            }

            // Convertir payload a JsonElement
            var payloadJson = JsonSerializer.Serialize(cloverMessage.Payload);
            Log.Debug("ConvertToTransactionResponse: Raw payload: {Payload}", payloadJson);
            
            JsonElement payload;
            
            // El payload puede venir como string (stringified JSON) o como objeto directo
            if (cloverMessage.Payload is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    // Payload es un string JSON, necesita parsearse
                    var innerJson = element.GetString();
                    if (!string.IsNullOrEmpty(innerJson))
                    {
                        payload = JsonDocument.Parse(innerJson).RootElement;
                        Log.Debug("ConvertToTransactionResponse: Parsed inner payload from string");
                    }
                    else
                    {
                        payload = element;
                    }
                }
                else
                {
                    payload = element;
                }
            }
            else
            {
                payload = JsonDocument.Parse(payloadJson).RootElement;
            }
            
            // Determinar si fue exitoso - según documento de Clover:
            // Campo "result" == "SUCCESS" indica éxito
            // También verificar campo "txState" para compatibilidad con diferentes versiones
            bool success = false;
            
            // Opción 1: Verificar campo "result" (Network Pay Display API - Documento de integración)
            if (payload.TryGetProperty("result", out var resultProp))
            {
                var resultValue = resultProp.GetString();
                success = string.Equals(resultValue, "SUCCESS", StringComparison.OrdinalIgnoreCase);
                Log.Information("ConvertToTransactionResponse: result = {Result}, success = {Success}", resultValue, success);
            }
            // Opción 2: Verificar campo "txState" (WebSocket API alternativa)
            else if (payload.TryGetProperty("txState", out var txState))
            {
                var txStateValue = txState.GetString();
                success = string.Equals(txStateValue, "SUCCESS", StringComparison.OrdinalIgnoreCase);
                Log.Information("ConvertToTransactionResponse: txState = {TxState}, success = {Success}", txStateValue, success);
            }
            // Opción 3: Verificar método del mensaje
            else if (cloverMessage.Method == "TX_STATE" || cloverMessage.Method == "TX_START_RESPONSE")
            {
                // Si hay un objeto payment, considerarlo como exitoso
                if (payload.TryGetProperty("payment", out _))
                {
                    success = true;
                    Log.Information("ConvertToTransactionResponse: Payment object found, assuming success");
                }
            }

            // Extraer razón si hay error
            string? reason = null;
            if (payload.TryGetProperty("reason", out var reasonProp))
            {
                reason = reasonProp.GetString();
            }
            if (payload.TryGetProperty("message", out var messageProp))
            {
                var msg = messageProp.GetString();
                if (string.IsNullOrEmpty(reason))
                    reason = msg;
            }

            // Extraer externalPaymentId (número de factura si se envió en request)
            string? externalPaymentId = null;
            if (payload.TryGetProperty("externalPaymentId", out var extPayIdProp))
            {
                externalPaymentId = extPayIdProp.GetString();
            }

            // Crear Payment si hay datos
            // Según documento Clover: el objeto "payment" puede estar en la raíz o 
            // la información puede venir directamente en el payload
            PaymentInfo? payment = null;
            
            if (payload.TryGetProperty("payment", out var paymentProp))
            {
                // Estructura con objeto "payment" anidado
                payment = ExtractPaymentInfo(paymentProp);
                Log.Information("ConvertToTransactionResponse: Extracted payment from 'payment' property");
            }
            else if (payload.TryGetProperty("id", out _) && payload.TryGetProperty("cardTransaction", out _))
            {
                // La información de pago está directamente en el payload raíz
                // Esto ocurre con Network Pay Display API según el documento
                payment = ExtractPaymentInfo(payload);
                Log.Information("ConvertToTransactionResponse: Extracted payment from root payload");
            }
            else if (payload.TryGetProperty("id", out var idProp) && success)
            {
                // Solo tenemos ID pero fue exitoso - crear PaymentInfo básico
                payment = new PaymentInfo
                {
                    Id = idProp.GetString() ?? string.Empty,
                    ExternalPaymentId = externalPaymentId
                };
                
                // Intentar extraer datos de tarjeta si están disponibles
                if (payload.TryGetProperty("cardTransaction", out var cardTxProp))
                {
                    payment.CardTransaction = ExtractCardTransaction(cardTxProp);
                }
                
                // Extraer amount
                if (payload.TryGetProperty("amount", out var amountProp))
                {
                    payment.Amount = amountProp.GetInt64();
                }
                
                Log.Information("ConvertToTransactionResponse: Created basic PaymentInfo with id: {Id}", payment.Id);
            }

            // Actualizar externalPaymentId en el payment si lo extrajimos
            if (payment != null && !string.IsNullOrEmpty(externalPaymentId) && string.IsNullOrEmpty(payment.ExternalPaymentId))
            {
                payment.ExternalPaymentId = externalPaymentId;
            }

            var response = new TransactionResponse
            {
                Success = success,
                Reason = reason,
                Payment = payment,
                Message = cloverMessage.Method,
                ExternalPaymentId = externalPaymentId
            };

            Log.Information("ConvertToTransactionResponse: Final response - Success={Success}, PaymentId={PaymentId}, AuthCode={AuthCode}, Card={Card}",
                response.Success,
                response.Payment?.Id ?? "N/A",
                response.Payment?.CardTransaction?.AuthCode ?? "N/A",
                response.Payment?.CardTransaction != null 
                    ? $"{response.Payment.CardTransaction.CardType} {response.Payment.CardTransaction.Last4}" 
                    : "N/A");

            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error converting CloverMessage to TransactionResponse");
            return new TransactionResponse
            {
                Success = false,
                Reason = ex.Message
            };
        }
    }

    /// <summary>
    /// Extrae información de cardTransaction desde JsonElement
    /// </summary>
    private CardTransaction ExtractCardTransaction(JsonElement cardTxElement)
    {
        return new CardTransaction
        {
            Last4 = cardTxElement.TryGetProperty("last4", out var last4) ? last4.GetString() : null,
            CardType = cardTxElement.TryGetProperty("cardType", out var cardType) ? cardType.GetString() : null,
            AuthCode = cardTxElement.TryGetProperty("authCode", out var authCode) ? authCode.GetString() : null,
            EntryType = cardTxElement.TryGetProperty("entryType", out var entryType) ? entryType.GetString() : null,
            Type = cardTxElement.TryGetProperty("type", out var type) ? type.GetString() : null,
            ReferenceId = cardTxElement.TryGetProperty("referenceId", out var refId) ? refId.GetString() : null,
            TransactionNo = cardTxElement.TryGetProperty("transactionNo", out var txNo) ? txNo.GetString() : null,
            First6 = cardTxElement.TryGetProperty("first6", out var first6) ? first6.GetString() : null
        };
    }

    /// <summary>
    /// Extrae información de pago desde JsonElement
    /// Según documento de integración Clover:
    /// - id = ID de transacción real (para refunds)
    /// - orderId = ID de la orden
    /// - cardTransaction.authCode = número de cupón
    /// - cardTransaction.cardType + last4 = tarjeta
    /// - externalPaymentId = número de factura
    /// </summary>
    private PaymentInfo ExtractPaymentInfo(JsonElement paymentElement)
    {
        var payment = new PaymentInfo
        {
            Id = paymentElement.TryGetProperty("id", out var id) ? id.GetString() : null
        };

        // Extraer amount (en centavos)
        if (paymentElement.TryGetProperty("amount", out var amount))
        {
            payment.Amount = amount.GetInt64();
        }

        // Extraer tipAmount (en centavos)
        if (paymentElement.TryGetProperty("tipAmount", out var tip))
        {
            payment.TipAmount = tip.GetInt64();
        }

        // Extraer taxAmount (en centavos)
        if (paymentElement.TryGetProperty("taxAmount", out var tax))
        {
            // Podríamos agregar este campo si es necesario
            // payment.TaxAmount = tax.GetInt64();
        }

        // Extraer orderId - puede venir como objeto o string directo
        if (paymentElement.TryGetProperty("order", out var order))
        {
            payment.Order = new OrderInfo
            {
                Id = order.TryGetProperty("id", out var orderId) ? orderId.GetString() : null
            };
        }
        else if (paymentElement.TryGetProperty("orderId", out var orderIdDirect))
        {
            payment.Order = new OrderInfo
            {
                Id = orderIdDirect.GetString()
            };
        }

        // Extraer externalPaymentId (número de factura enviado en el request)
        if (paymentElement.TryGetProperty("externalPaymentId", out var extPayId))
        {
            payment.ExternalPaymentId = extPayId.GetString();
        }

        // Extraer cardTransaction usando el método dedicado
        if (paymentElement.TryGetProperty("cardTransaction", out var cardTx))
        {
            payment.CardTransaction = ExtractCardTransaction(cardTx);
        }

        // Extraer device info si está disponible
        if (paymentElement.TryGetProperty("device", out var device))
        {
            if (device.TryGetProperty("id", out var deviceId))
            {
                // Podríamos guardar el deviceId si es necesario
                Log.Debug("Payment from device: {DeviceId}", deviceId.GetString());
            }
        }

        // Extraer createdTime si está disponible
        if (paymentElement.TryGetProperty("createdTime", out var createdTime))
        {
            // Clover envía timestamp en milisegundos
            try
            {
                var timestamp = createdTime.GetInt64();
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                Log.Debug("Payment created at: {DateTime}", dateTime);
            }
            catch { /* Ignorar si no se puede parsear */ }
        }

        Log.Debug("ExtractPaymentInfo: ID={Id}, Amount={Amount}, ExternalId={ExtId}, Card={CardType} {Last4}, AuthCode={AuthCode}",
            payment.Id,
            payment.Amount,
            payment.ExternalPaymentId ?? "N/A",
            payment.CardTransaction?.CardType ?? "N/A",
            payment.CardTransaction?.Last4 ?? "N/A",
            payment.CardTransaction?.AuthCode ?? "N/A");

        return payment;
    }

    /// <summary>
    /// Limpia la carpeta INBOX eliminando el archivo procesado
    /// </summary>
    public async Task<bool> DeleteInboxFileAsync(string filename)
    {
        try
        {
            var config = _configService.GetConfig();
            var filePath = Path.Combine(config.Folders.Inbox, filename);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                Log.Information("INBOX file deleted: {Filename}", filename);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting INBOX file: {Filename}", filename);
            return false;
        }
    }

    /// <summary>
    /// Limpia todos los archivos de la carpeta INBOX
    /// </summary>
    public async Task<int> CleanupInboxAsync()
    {
        try
        {
            var config = _configService.GetConfig();
            var inboxPath = config.Folders.Inbox;

            if (!Directory.Exists(inboxPath))
            {
                return 0;
            }

            var files = Directory.GetFiles(inboxPath, "*.json");
            int deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    await Task.Run(() => File.Delete(file));
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error deleting file: {File}", file);
                }
            }

            Log.Information("INBOX cleaned: {DeletedCount}/{TotalCount} files removed", deletedCount, files.Length);
            return deletedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning INBOX");
            return 0;
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