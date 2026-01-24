using CloverBridge.Models;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CloverBridge.Services;

/// <summary>
/// Monitorea carpeta INBOX para solicitudes del ERP
/// </summary>
public class InboxWatcherService : BackgroundService
{
    private readonly ConfigurationService _configService;
    private readonly CloverWebSocketService _cloverService;
    private readonly TransactionQueueService _queueService;
    private readonly TransactionLogService _logService;
    private readonly MercadoPagoService _mpService;
    private FileSystemWatcher? _watcher;
    
    // Diccionario para rastrear transacciones en proceso
    private readonly ConcurrentDictionary<string, TransactionFile> _activeTransactions = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _transactionTimeouts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TransactionResponse>> _mpPendingPayments = new();

    public InboxWatcherService(
        ConfigurationService configService,
        CloverWebSocketService cloverService,
        TransactionQueueService queueService,
        MercadoPagoService mpService)
    {
        _configService = configService;
        _cloverService = cloverService;
        _queueService = queueService;
        _mpService = mpService;
        _logService = new TransactionLogService(configService);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configService.GetConfig();
        var inboxPath = config.Folders.Inbox;

        Directory.CreateDirectory(inboxPath);

        // Watcher para archivos JSON
        _watcher = new FileSystemWatcher(inboxPath)
        {
            Filter = "*.*", // Monitorear todos los archivos
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged; // También detectar cambios

        Log.Information("InboxWatcher started, monitoring: {Path} (*.json, cobro.txt)", inboxPath);

        // Procesar archivos existentes
        ProcessExistingFiles(inboxPath);

        return Task.CompletedTask;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Solo procesar cobro.txt cuando cambia
        if (Path.GetFileName(e.FullPath).Equals("cobro.txt", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("File changed detected: {Path}", e.FullPath);
            _ = ProcessFileAsync(e.FullPath);
        }
    }

    private void ProcessExistingFiles(string inboxPath)
    {
        try
        {
            // Buscar archivos JSON
            var jsonFiles = Directory.GetFiles(inboxPath, "*.json");
            
            // Buscar cobro.txt específicamente
            var cobroFile = Path.Combine(inboxPath, "cobro.txt");
            var hasCobro = File.Exists(cobroFile);
            
            var totalFiles = jsonFiles.Length + (hasCobro ? 1 : 0);
            Log.Information("Found {Count} existing files in inbox (JSON: {JsonCount}, cobro.txt: {HasCobro})", 
                totalFiles, jsonFiles.Length, hasCobro);

            // Procesar archivos JSON
            foreach (var file in jsonFiles)
            {
                _ = ProcessFileAsync(file);
            }
            
            // Procesar cobro.txt si existe
            if (hasCobro)
            {
                _ = ProcessFileAsync(cobroFile);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing existing files");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        
        // Solo procesar archivos .json o cobro.txt
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("cobro.txt", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("New file detected: {Path}", e.FullPath);
            _ = ProcessFileAsync(e.FullPath);
        }
    }
    // Prevenir procesamiento duplicado de archivos
    private readonly ConcurrentDictionary<string, DateTime> _processingFiles = new();

    private async Task ProcessFileAsync(string filePath)
    {
        CancellationTokenSource? timeoutCts = null;
        
        try
        {
            // Prevenir procesamiento duplicado (debounce de 2 segundos)
            var fileName = Path.GetFileName(filePath);
            if (_processingFiles.TryGetValue(fileName, out var lastProcessed))
            {
                if ((DateTime.Now - lastProcessed).TotalSeconds < 2)
                {
                    Log.Debug("Skipping duplicate processing for: {Path}", filePath);
                    return;
                }
            }
            _processingFiles[fileName] = DateTime.Now;

            // Esperar a que el archivo esté completamente escrito
            await Task.Delay(500);

            // Verificar que el archivo existe
            if (!File.Exists(filePath))
            {
                Log.Warning("File no longer exists: {Path}", filePath);
                return;
            }

            Log.Information("Processing file: {Path}", filePath);

            // Leer contenido del archivo
            var json = await File.ReadAllTextAsync(filePath);
            Log.Debug("File content ({Length} bytes): {Content}", json.Length, 
                json.Length > 500 ? json.Substring(0, 500) + "..." : json);

            // Parsear la transacción con opciones flexibles
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Aceptar tanto camelCase como PascalCase
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            TransactionFile? transaction = null;
            try
            {
                transaction = JsonSerializer.Deserialize<TransactionFile>(json, options);
            }
            catch (JsonException jsonEx)
            {
                Log.Error(jsonEx, "JSON parse error for file: {Path}", filePath);
                await FinalizeTransactionAndCleanup(null, filePath, TransactionStatus.Failed, $"Error parseando JSON: {jsonEx.Message}");
                return;
            }

            if (transaction == null)
            {
                Log.Warning("Invalid transaction file (null after deserialize): {Path}", filePath);
                await FinalizeTransactionAndCleanup(null, filePath, TransactionStatus.Failed, "Archivo de transacción inválido (deserialización nula)");
                return;
            }

            Log.Information("Transaction parsed: InvoiceNumber={Invoice}, Amount={Amount}, Type={Type}", 
                transaction.InvoiceNumber, transaction.Amount, transaction.Type);

            // Validar datos mínimos requeridos
            if (string.IsNullOrEmpty(transaction.InvoiceNumber) || transaction.Amount <= 0)
            {
                Log.Warning("Invalid transaction data: InvoiceNumber={Invoice} Amount={Amount}", 
                    transaction.InvoiceNumber, transaction.Amount);
                
                transaction.Status = TransactionStatus.Failed;
                transaction.ErrorMessage = $"Datos de transacción inválidos: Invoice='{transaction.InvoiceNumber}', Amount={transaction.Amount}";
                transaction.AddLogEntry("VALIDATION_ERROR", "Datos inválidos", $"Invoice: {transaction.InvoiceNumber}, Amount: {transaction.Amount}");
                
                await FinalizeTransactionAndCleanup(transaction, filePath, TransactionStatus.Failed, transaction.ErrorMessage);
                return;
            }

            // Asegurar IDs únicos
            if (string.IsNullOrEmpty(transaction.TransactionId))
                transaction.TransactionId = Guid.NewGuid().ToString();
            
            // El ExternalId/ExternalPaymentId DEBE ser único para cada transacción en Clover
            // Si no viene o parece un valor genérico (como "visa", "mastercard", etc.), generamos uno único
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            if (string.IsNullOrEmpty(transaction.ExternalId) || 
                IsGenericPaymentMethod(transaction.ExternalId))
            {
                // Generar ID único basado en InvoiceNumber + timestamp + guid parcial
                transaction.ExternalId = $"{transaction.InvoiceNumber}-{timestamp}-{uniqueSuffix}";
                Log.Information("Generated unique ExternalId: {ExternalId}", transaction.ExternalId);
            }
            else
            {
                // Incluso si viene un externalId, agregamos sufijo para garantizar unicidad
                transaction.ExternalId = $"{transaction.ExternalId}-{timestamp}-{uniqueSuffix}";
                Log.Information("Made ExternalId unique: {ExternalId}", transaction.ExternalId);
            }

            // Guardar referencia al archivo INBOX
            transaction.InboxFilePath = filePath;
            transaction.ProcessStartTime = DateTime.UtcNow;
            transaction.Status = TransactionStatus.Pending;
            transaction.Timestamp = DateTime.UtcNow;
            
            transaction.AddLogEntry("RECEIVED", "Transacción recibida en INBOX", $"Archivo: {Path.GetFileName(filePath)}");

            // Registrar transacción activa
            _activeTransactions[transaction.TransactionId] = transaction;

            // Crear timeout de 75 segundos
            timeoutCts = new CancellationTokenSource();
            _transactionTimeouts[transaction.TransactionId] = timeoutCts;

            // Determinar PROVEEDOR (Clover vs QRMP)
            var config = _configService.GetConfig();
            if (string.IsNullOrEmpty(transaction.Provider))
            {
                transaction.Provider = config.PaymentProvider ?? "CLOVER";
            }
            
            Log.Information("Routing transaction for {Invoice} to provider: {Provider}", 
                transaction.InvoiceNumber, transaction.Provider);

            // Enviar solicitud según proveedor
            transaction.Status = TransactionStatus.Processing;
            transaction.SentToTerminalTime = DateTime.UtcNow;
            transaction.TimeoutRemainingSeconds = 80;
            
            Task<TransactionResponse> paymentTask;
            
            if (transaction.Provider.Equals("QRMP", StringComparison.OrdinalIgnoreCase))
            {
                if (!config.Qrmp.Enabled)
                {
                    Log.Warning("⚠️ Cobro QRMP solicitado pero la integración está desactivada");
                    await FinalizeTransactionAndCleanup(transaction, filePath, TransactionStatus.Failed, "Integración Mercado Pago desactivada");
                    return;
                }

                transaction.AddLogEntry("MP_ORDER_CREATING", "Creando orden en Mercado Pago (QR Fijo)");
                var success = await _mpService.CreateOrderAsync(transaction);
                
                if (!success)
                {
                    await FinalizeTransactionAndCleanup(transaction, filePath, TransactionStatus.Failed, "Error al crear orden en Mercado Pago");
                    return;
                }
                
                transaction.AddLogEntry("MP_ORDER_CREATED", "Orden creada, esperando escaneo y pago");
                
                // Crear Task que se completará vía Webhook
                var tcs = new TaskCompletionSource<TransactionResponse>();
                _mpPendingPayments[transaction.InvoiceNumber] = tcs;
                paymentTask = tcs.Task;
            }
            else
            {
                if (!config.Clover.Enabled)
                {
                    Log.Warning("⚠️ Cobro Clover solicitado pero la integración está desactivada");
                    await FinalizeTransactionAndCleanup(transaction, filePath, TransactionStatus.Failed, "Integración Clover desactivada");
                    return;
                }

                // Default: CLOVER
                transaction.AddLogEntry("SENT_TO_TERMINAL", "Solicitud enviada a terminal Clover", $"Monto en centavos: {(long)(transaction.Amount * 100)}");
                
                // Envolver CloverTask para devolver TransactionResponse
                paymentTask = Task.Run(async () => {
                    var cloverMsg = await _cloverService.SendSaleAsync(transaction.Amount, transaction.ExternalId, 0);
                    return ConvertToTransactionResponse(cloverMsg);
                });
            }

            // Tarea de timeout (80 segundos)
            var timeoutTask = CountdownWithUpdatesAsync(transaction, 80, timeoutCts.Token);

            // Esperar el primero que termine
            var completedTask = await Task.WhenAny(paymentTask, timeoutTask);

            if (completedTask == timeoutTask && !timeoutCts.Token.IsCancellationRequested)
            {
                // TIMEOUT - 80 segundos sin respuesta confirmada
                Log.Warning("⏱️ Transaction TIMEOUT: Invoice={Invoice} Provider={Provider}", 
                    transaction.InvoiceNumber, transaction.Provider);
                
                transaction.AddLogEntry("TIMEOUT", "Timeout de 80 segundos alcanzado", "Pago no confirmado en tiempo límite");
                
                if (transaction.Provider.Equals("CLOVER", StringComparison.OrdinalIgnoreCase))
                {
                    // ENVIAR CANCELACIÓN AL TERMINAL CLOVER
                    try { await _cloverService.CancelTransactionAsync(); } catch { }
                }
                else
                {
                    // LIMPIAR ORDEN EN MP
                    try { await _mpService.DeleteOrderAsync(); } catch { }
                    _mpPendingPayments.TryRemove(transaction.InvoiceNumber, out _);
                }
                
                await FinalizeTransactionAndCleanup(transaction, filePath, TransactionStatus.Timeout, 
                    "Timeout de 80 segundos - Pago no confirmado");
            }
            else if (!timeoutCts.Token.IsCancellationRequested)
            {
                // Respuesta recibida antes del timeout
                try { timeoutCts.Cancel(); } catch { }
                
                if (transaction.Provider.Equals("QRMP", StringComparison.OrdinalIgnoreCase))
                {
                    _mpPendingPayments.TryRemove(transaction.InvoiceNumber, out _);
                }

                var response = await paymentTask;
                transaction.AddLogEntry("RESPONSE_RECEIVED", "Respuesta de pago recibida", $"Exitosa: {response.Success}");

                // Procesar resultado
                var fileService = new TransactionFileService(_configService);
                fileService.ProcessPaymentResult(transaction, response);
                
                transaction.AddLogEntry("RESULT_PROCESSED", "Resultado procesado", $"Estado: {transaction.Status}");

                // Finalizar y limpiar
                await FinalizeTransactionAndCleanup(transaction, filePath, transaction.Status, transaction.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing file: {Path}", filePath);
            
            // Cancelar timeout si existe
            if (timeoutCts != null)
            {
                timeoutCts.Cancel();
                timeoutCts.Dispose();
            }
            
            // Intentar crear archivo de error en OUTBOX
            try
            {
                var errorTransaction = new TransactionFile
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    ExternalId = Path.GetFileNameWithoutExtension(filePath),
                    InvoiceNumber = Path.GetFileNameWithoutExtension(filePath),
                    Status = TransactionStatus.Failed,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow,
                    ProcessStartTime = DateTime.UtcNow,
                    ProcessEndTime = DateTime.UtcNow
                };
                
                errorTransaction.AddLogEntry("ERROR", "Error procesando archivo", ex.Message);
                
                await FinalizeTransactionAndCleanup(errorTransaction, filePath, TransactionStatus.Failed, ex.Message);
            }
            catch (Exception innerEx)
            {
                Log.Error(innerEx, "Error creating error transaction file");
                await DeleteFileAsync(filePath);
            }
        }
    }

    /// <summary>
    /// Cuenta regresiva de timeout (sin escribir a OUTBOX durante el proceso)
    /// </summary>
    private async Task CountdownWithUpdatesAsync(TransactionFile transaction, int totalSeconds, CancellationToken cancellationToken)
    {
        for (int remaining = totalSeconds; remaining >= 0; remaining--)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Actualizar tiempo restante (solo en memoria)
            transaction.TimeoutRemainingSeconds = remaining;

            // Log cada 15 segundos para monitoreo
            if (remaining % 15 == 0 && remaining > 0)
            {
                Log.Debug("Transaction countdown: Invoice={Invoice} Remaining={Remaining}s", 
                    transaction.InvoiceNumber, remaining);
            }

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Finaliza una transacción: guarda en OUTBOX, registra en log y limpia INBOX
    /// </summary>
    private async Task FinalizeTransactionAndCleanup(TransactionFile? transaction, string inboxFilePath, TransactionStatus finalStatus, string? errorMessage = null)
    {
        try
        {
            if (transaction != null)
            {
                // Actualizar estado final
                transaction.Status = finalStatus;
                transaction.ProcessEndTime = DateTime.UtcNow;
                transaction.TimeoutRemainingSeconds = null; // Limpiar contador
                
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    transaction.ErrorMessage = errorMessage;
                }

                // Calcular duración
                var duration = transaction.ProcessEndTime.Value - (transaction.ProcessStartTime ?? transaction.Timestamp);
                transaction.AddLogEntry("FINALIZED", $"Transacción finalizada con estado: {finalStatus}", $"Duración: {duration.TotalSeconds:F2}s");

                // Guardar en OUTBOX con toda la información
                await WriteTransactionToOutboxAsync(transaction);

                // Registrar en log histórico
                await _logService.LogTransactionAsync(transaction);

                // Limpiar de transacciones activas
                _activeTransactions.TryRemove(transaction.TransactionId, out _);
                
                if (_transactionTimeouts.TryRemove(transaction.TransactionId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                Log.Information("Transaction finalized: Invoice={Invoice} Status={Status} Duration={Duration}s",
                    transaction.InvoiceNumber, finalStatus, duration.TotalSeconds);
            }

            // Eliminar archivo de INBOX solo cuando la transacción está completa
            await DeleteFileAsync(inboxFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error finalizing transaction and cleanup: {Path}", inboxFilePath);
        }
    }

    private async Task WriteTransactionToOutboxAsync(TransactionFile transaction)
    {
        try
        {
            var config = _configService.GetConfig();
            var invoiceNum = transaction.InvoiceNumber ?? "unknown";
            
            // Serializar transacción a JSON
            var json = JsonSerializer.Serialize(transaction, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // 1. GUARDAR COMO cobro.txt en OUTBOX (archivo principal que lee el ERP)
            var cobroPath = Path.Combine(config.Folders.Outbox, "cobro.txt");
            await File.WriteAllTextAsync(cobroPath, json);
            Log.Information("✅ Resultado guardado en OUTBOX: cobro.txt Status={Status}", transaction.Status);

            // 2. GUARDAR COPIA CON INVOICE NUMBER en ARCHIVE (historial)
            var archivePath = config.Folders.Archive;
            Directory.CreateDirectory(archivePath); // Crear carpeta ARCHIVE si no existe
            
            var invoiceFilename = GetUniqueInvoiceFilename(archivePath, invoiceNum);
            var invoiceFullPath = Path.Combine(archivePath, invoiceFilename);
            await File.WriteAllTextAsync(invoiceFullPath, json);
            Log.Information("📁 Copia guardada en ARCHIVE: {Filename} Status={Status} LogEntries={LogCount}", 
                invoiceFilename, transaction.Status, transaction.TransactionLog.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing transaction files: {Invoice}", transaction.InvoiceNumber);
        }
    }

    /// <summary>
    /// Genera un nombre de archivo único para el invoice number.
    /// Si ya existe, agrega _2, _3, etc.
    /// </summary>
    private string GetUniqueInvoiceFilename(string outboxPath, string invoiceNumber)
    {
        var baseFilename = $"{invoiceNumber}.txt";
        var fullPath = Path.Combine(outboxPath, baseFilename);
        
        // Si no existe, usar el nombre base
        if (!File.Exists(fullPath))
        {
            return baseFilename;
        }
        
        // Buscar un número disponible
        int suffix = 2;
        while (true)
        {
            var newFilename = $"{invoiceNumber}_{suffix}.txt";
            var newPath = Path.Combine(outboxPath, newFilename);
            
            if (!File.Exists(newPath))
            {
                Log.Information("Invoice file already exists, using: {Filename}", newFilename);
                return newFilename;
            }
            
            suffix++;
            
            // Límite de seguridad
            if (suffix > 1000)
            {
                // Si hay más de 1000 duplicados, usar timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                return $"{invoiceNumber}_{timestamp}.txt";
            }
        }
    }

    private async Task DeleteFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                Log.Information("INBOX file deleted: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting file: {Path}", filePath);
        }
    }

    public override void Dispose()
    {
        // Limpiar timeouts activos
        foreach (var cts in _transactionTimeouts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _transactionTimeouts.Clear();
        _activeTransactions.Clear();
        
        _watcher?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Obtiene el estado de las transacciones activas
    /// </summary>
    public object GetActiveTransactionsStatus()
    {
        return new
        {
            activeCount = _activeTransactions.Count,
            transactions = _activeTransactions.Values.Select(t => new
            {
                transactionId = t.TransactionId,
                invoiceNumber = t.InvoiceNumber,
                status = t.Status.ToString(),
                amount = t.Amount,
                processStartTime = t.ProcessStartTime,
                elapsedSeconds = t.ProcessStartTime.HasValue 
                    ? (DateTime.UtcNow - t.ProcessStartTime.Value).TotalSeconds 
                    : 0
            }).ToList()
        };
    }

    /// <summary>
    /// Se llama cuando Mercado Pago confirma un pago vía Webhook
    /// </summary>
    public async Task CompleteMercadoPagoPaymentAsync(string paymentId)
    {
        try
        {
            Log.Information("MP Webhook: Procesando pago {PaymentId}", paymentId);
            
            // 1. Consultar detalles reales del pago en MP (Server-to-Server)
            var mpPayment = await _mpService.GetPaymentAsync(paymentId);
            if (mpPayment == null)
            {
                Log.Warning("MP Webhook: No se pudieron obtener detalles del pago {PaymentId}", paymentId);
                return;
            }

            // 2. Extraer external_reference para matchear con InvoiceNúmero
            string? invoiceNumber = null;
            if (mpPayment.Value.TryGetProperty("external_reference", out var extRef))
            {
                invoiceNumber = extRef.GetString();
            }

            if (string.IsNullOrEmpty(invoiceNumber))
            {
                Log.Warning("MP Webhook: El pago {PaymentId} no tiene external_reference", paymentId);
                return;
            }

            // 3. Buscar si tenemos una transacción activa esperando este pago
            if (_mpPendingPayments.TryGetValue(invoiceNumber, out var tcs))
            {
                Log.Information("MP Webhook: Match encontrado para Factura {Invoice}", invoiceNumber);
                
                // Convertir el pago de MP a nuestro formato interno de respuesta
                var response = ConvertMpPaymentToResponse(mpPayment.Value);
                
                // Completar el Task que está esperando en ProcessFileAsync
                tcs.TrySetResult(response);
            }
            else
            {
                Log.Information("MP Webhook: No hay transacciones locales esperando el pago de factura {Invoice}", invoiceNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MP Webhook: Error procesando completitud de pago {PaymentId}", paymentId);
        }
    }

    private TransactionResponse ConvertMpPaymentToResponse(JsonElement mp)
    {
        var status = mp.TryGetProperty("status", out var s) ? s.GetString() : "unknown";
        var statusDetail = mp.TryGetProperty("status_detail", out var sd) ? sd.GetString() : "";
        var success = status == "approved";
        
        var response = new TransactionResponse
        {
            Success = success,
            Result = status?.ToUpper() ?? "FAILED",
            Reason = statusDetail,
            Message = $"Mercado Pago: {status} ({statusDetail})"
        };

        if (mp.TryGetProperty("id", out var idProp))
        {
            var id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString();
            
            response.Payment = new PaymentInfo
            {
                Id = id ?? "",
                Amount = mp.TryGetProperty("transaction_amount", out var amt) ? (long)(amt.GetDecimal() * 100) : 0,
                ExternalPaymentId = mp.TryGetProperty("external_reference", out var ext) ? ext.GetString() : null
            };

            // Llenar detalles específicos de MP en el objeto que se persistirá
            response.Payment.Note = $"MP Status: {status}, Detail: {statusDetail}";
            
            // Usar el objeto MPDetail que agregamos a PaymentFileInfo
            // Lo pasamos vía PaymentInfo para que ProcessPaymentResult lo use
            // NOTA: Como PaymentInfo en CloverMessages no tiene el campo MP, 
            // podemos usar TransactionResponse.Message o Raw para pasar info extra
            // o simplemente confiar en que ProcessPaymentResult lo maneje.
        }

        return response;
    }

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
            Log.Debug("ConvertToTransactionResponse: Raw payload: {Payload}", 
                payloadJson.Length > 500 ? payloadJson.Substring(0, 500) + "..." : payloadJson);
            
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
            
            // Determinar si fue exitoso según el método del mensaje y el campo result
            bool success = false;
            
            // FINISH_OK = Pago exitoso confirmado
            // FINISH_CANCEL = Pago cancelado
            if (cloverMessage.Method == "FINISH_OK")
            {
                success = true;
                Log.Information("ConvertToTransactionResponse: FINISH_OK = Pago exitoso");
            }
            else if (cloverMessage.Method == "FINISH_CANCEL")
            {
                success = false;
                Log.Information("ConvertToTransactionResponse: FINISH_CANCEL = Pago cancelado");
            }
            // También verificar result en el payload o dentro del payment
            else
            {
                // El payment puede estar stringificado, necesitamos parsearlo
                JsonElement? paymentElementCheck = null;
                
                if (payload.TryGetProperty("payment", out var paymentPropCheck))
                {
                    if (paymentPropCheck.ValueKind == JsonValueKind.String)
                    {
                        var paymentStrCheck = paymentPropCheck.GetString();
                        if (!string.IsNullOrEmpty(paymentStrCheck))
                        {
                            try
                            {
                                paymentElementCheck = JsonDocument.Parse(paymentStrCheck).RootElement;
                                Log.Debug("ConvertToTransactionResponse: Payment parseado desde string");
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        paymentElementCheck = paymentPropCheck;
                    }
                }
                
                // Buscar result en el payment parseado
                if (paymentElementCheck.HasValue && paymentElementCheck.Value.TryGetProperty("result", out var resultInPayment))
                {
                    var resultValue = resultInPayment.GetString();
                    success = string.Equals(resultValue, "SUCCESS", StringComparison.OrdinalIgnoreCase);
                    Log.Information("ConvertToTransactionResponse: payment.result = {Result}, success = {Success}", resultValue, success);
                }
                // Buscar result en el payload principal
                else if (payload.TryGetProperty("result", out var resultProp))
                {
                    var resultValue = resultProp.GetString();
                    success = string.Equals(resultValue, "SUCCESS", StringComparison.OrdinalIgnoreCase);
                    Log.Information("ConvertToTransactionResponse: result = {Result}, success = {Success}", resultValue, success);
                }
                // Buscar txState para compatibilidad
                else if (payload.TryGetProperty("txState", out var txState))
                {
                    var txStateValue = txState.GetString();
                    success = string.Equals(txStateValue, "SUCCESS", StringComparison.OrdinalIgnoreCase);
                    Log.Information("ConvertToTransactionResponse: txState = {TxState}, success = {Success}", txStateValue, success);
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
            PaymentInfo? payment = null;
            
            if (payload.TryGetProperty("payment", out var paymentProp))
            {
                // El payment puede venir como string (stringify) o como objeto
                JsonElement paymentElement;
                
                if (paymentProp.ValueKind == JsonValueKind.String)
                {
                    var paymentStr = paymentProp.GetString();
                    if (!string.IsNullOrEmpty(paymentStr))
                    {
                        try
                        {
                            paymentElement = JsonDocument.Parse(paymentStr).RootElement;
                            Log.Information("ConvertToTransactionResponse: Payment parsed from stringified JSON");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error parsing stringified payment");
                            paymentElement = paymentProp;
                        }
                    }
                    else
                    {
                        paymentElement = paymentProp;
                    }
                }
                else
                {
                    paymentElement = paymentProp;
                }
                
                payment = ExtractPaymentInfo(paymentElement);
                Log.Information("ConvertToTransactionResponse: Extracted payment - ID={Id}, AuthCode={AuthCode}", 
                    payment.Id, payment.CardTransaction?.AuthCode ?? "N/A");
            }
            else if (payload.TryGetProperty("id", out _) && payload.TryGetProperty("cardTransaction", out _))
            {
                // La información de pago está directamente en el payload raíz
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
                
                if (payload.TryGetProperty("cardTransaction", out var cardTxProp))
                {
                    payment.CardTransaction = ExtractCardTransaction(cardTxProp);
                }
                
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

            Log.Information("ConvertToTransactionResponse: Final response - Success={Success}, PaymentId={PaymentId}, Reason={Reason}",
                response.Success,
                response.Payment?.Id ?? "N/A",
                response.Reason ?? "N/A");

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

    private PaymentInfo ExtractPaymentInfo(JsonElement paymentElement)
    {
        var payment = new PaymentInfo
        {
            Id = paymentElement.TryGetProperty("id", out var id) ? id.GetString() : null
        };

        if (paymentElement.TryGetProperty("amount", out var amount))
        {
            payment.Amount = amount.GetInt64();
        }

        if (paymentElement.TryGetProperty("tipAmount", out var tip))
        {
            payment.TipAmount = tip.GetInt64();
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

        // Extraer cardTransaction
        if (paymentElement.TryGetProperty("cardTransaction", out var cardTx))
        {
            payment.CardTransaction = ExtractCardTransaction(cardTx);
        }

        // Extraer note (contiene info adicional de QR, Billetera, Lote, etc.)
        if (paymentElement.TryGetProperty("note", out var noteProp))
        {
            payment.Note = noteProp.GetString();
        }

        // Extraer result (SUCCESS, etc.)
        if (paymentElement.TryGetProperty("result", out var resultPropPayment))
        {
            payment.Result = resultPropPayment.GetString();
        }

        // Extraer createdTime
        if (paymentElement.TryGetProperty("createdTime", out var createdTimeProp))
        {
            payment.CreatedTime = createdTimeProp.GetInt64();
        }

        // Extraer transactionInfo (cardTypeLabel, entryType)
        if (paymentElement.TryGetProperty("transactionInfo", out var txInfo))
        {
            payment.TransactionInfo = new TransactionInfo
            {
                CardTypeLabel = txInfo.TryGetProperty("cardTypeLabel", out var cardLabel) ? cardLabel.GetString() : null,
                EntryType = txInfo.TryGetProperty("entryType", out var entryType) ? entryType.GetString() : null,
                FiscalInvoiceNumber = txInfo.TryGetProperty("fiscalInvoiceNumber", out var fiscalNum) ? fiscalNum.GetString() : null
            };
        }

        // Extraer tender
        if (paymentElement.TryGetProperty("tender", out var tenderProp))
        {
            payment.Tender = new TenderInfo
            {
                Id = tenderProp.TryGetProperty("id", out var tenderId) ? tenderId.GetString() : null,
                LabelKey = tenderProp.TryGetProperty("labelKey", out var labelKey) ? labelKey.GetString() : null
            };
        }

        // Extraer device
        if (paymentElement.TryGetProperty("device", out var deviceProp))
        {
            payment.Device = new DeviceInfo
            {
                Id = deviceProp.TryGetProperty("id", out var deviceId) ? deviceId.GetString() : null
            };
        }

        // Extraer merchant
        if (paymentElement.TryGetProperty("merchant", out var merchantProp))
        {
            payment.Merchant = new MerchantInfo
            {
                Id = merchantProp.TryGetProperty("id", out var merchantId) ? merchantId.GetString() : null
            };
        }

        // Extraer employee
        if (paymentElement.TryGetProperty("employee", out var employeeProp))
        {
            payment.Employee = new EmployeeInfo
            {
                Id = employeeProp.TryGetProperty("id", out var employeeId) ? employeeId.GetString() : null
            };
        }

        return payment;
    }

    /// <summary>
    /// Detecta si el externalId parece ser un método de pago genérico
    /// en lugar de un ID único de transacción
    /// </summary>
    private bool IsGenericPaymentMethod(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return true;
            
        var genericValues = new[]
        {
            "visa", "mastercard", "amex", "american express", "discover",
            "credit", "debit", "card", "tarjeta", "efectivo", "cash",
            "transfer", "transferencia", "cheque", "check",
            "payment", "pago", "sale", "venta", "test", "prueba"
        };
        
        var lowerValue = externalId.ToLowerInvariant().Trim();
        
        // Es genérico si coincide exactamente con algún valor común
        // o si es muy corto (menos de 5 caracteres)
        return genericValues.Any(g => lowerValue == g) || lowerValue.Length < 5;
    }
}

