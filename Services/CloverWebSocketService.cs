using CloverBridge.Models;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CloverBridge.Services;

/// <summary>
/// Servicio de WebSocket para comunicación con Clover
/// </summary>
public class CloverWebSocketService : BackgroundService
{
    private readonly ConfigurationService _configService;
    private ClientWebSocket? _webSocket;
    private ConnectionState _state = ConnectionState.Disconnected;
    private string? _lastPairingCode;
    private int _reconnectAttempts = 0;
    
    // Opciones de JSON serializador con convertidor personalizado
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonElementConverter() }
    };

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<string>? PairingCodeReceived;
    public event EventHandler<CloverMessage>? MessageReceived;

    public CloverWebSocketService(ConfigurationService configService)
    {
        _configService = configService;
    }

    public ConnectionState State => _state;
    public string? LastPairingCode => _lastPairingCode;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("CloverWebSocketService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(stoppingToken);
                await ReceiveMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in WebSocket connection");
                UpdateState(ConnectionState.Error);
                
                // Esperar antes de reconectar
                var config = _configService.GetConfig();
                if (_reconnectAttempts < config.Clover.MaxReconnectAttempts)
                {
                    _reconnectAttempts++;
                    Log.Information("Reconnect attempt {Attempt}/{Max}", 
                        _reconnectAttempts, config.Clover.MaxReconnectAttempts);
                    await Task.Delay(config.Clover.ReconnectDelayMs, stoppingToken);
                }
                else
                {
                    Log.Warning("Max reconnect attempts reached, waiting longer...");
                    await Task.Delay(60000, stoppingToken); // Esperar 1 minuto
                    _reconnectAttempts = 0;
                }
            }
        }

        await DisconnectAsync();
        Log.Information("CloverWebSocketService stopped");
    }

    // Exponer conexión manual para UI/control
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var config = _configService.GetConfig();
        var url = config.Clover.GetWebSocketUrl();

        Log.Information("Connecting to Clover at {Url}", url);
        UpdateState(ConnectionState.Connecting);

        _webSocket = new ClientWebSocket();
        
        // Configurar opciones del WebSocket
        if (config.Clover.Secure)
        {
            // Para wss://, permitir certificados autofirmados si es necesario
            _webSocket.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                // En producción, verificar el certificado apropiadamente
                // Por ahora, aceptamos cualquier certificado para desarrollo
                if (errors != System.Net.Security.SslPolicyErrors.None)
                {
                    Log.Warning("SSL certificate validation failed: {Errors}", errors);
                }
                return true; // Aceptar el certificado
            };
        }

        await _webSocket.ConnectAsync(new Uri(url), cancellationToken);

        UpdateState(ConnectionState.Connected);
        _reconnectAttempts = 0;
        Log.Information("Connected to Clover");

        // Enviar pairing request
        await SendPairingRequestAsync();
    }

    // Exponer pairing manual para UI/control
    public async Task SendPairingRequestAsync()
    {
        var config = _configService.GetConfig();

        Log.Information("Initiating pairing request. AuthToken present: {HasToken}", !string.IsNullOrEmpty(config.Clover.AuthToken));
        UpdateState(ConnectionState.PairingRequired);

        // Crear el payload interno como en TypeScript - SIEMPRE incluir authToken si existe
        var pairingRequestPayload = new
        {
            method = "PAIRING_REQUEST",
            serialNumber = config.Clover.SerialNumber ?? "CB-001",
            name = config.Clover.PosName ?? "CloverBridge-POS",
            authenticationToken = config.Clover.AuthToken ?? null
        };

        // Envolver en RemoteMessage como espera Clover (igual que TypeScript)
        var message = new
        {
            method = "PAIRING_REQUEST",
            payload = JsonSerializer.Serialize(pairingRequestPayload), // Stringify payload!
            remoteApplicationID = config.Clover.RemoteAppId ?? "com.mycompany.cloverbridge",
            remoteSourceSDK = "CloverBridge-1.0.0",
            version = 1
        };

        var json = JsonSerializer.Serialize(message);
        Log.Information("📤 Sending PAIRING_REQUEST: {Json}", json);
        Console.WriteLine($"📤 PAIRING REQUEST: {json}");
        
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None
        );

        // Timeout de 15 segundos - si no hay respuesta, usar modo simulado (igual que TypeScript)
        _ = Task.Delay(15000).ContinueWith(async t =>
        {
            if (State == ConnectionState.PairingRequired)
            {
                Log.Warning("⏱️ No response from terminal after 15s - using fallback mode");
                Console.WriteLine("⏱️ TIMEOUT - Terminal no respondió, usando modo simulado");

                // Generar código localmente
                var code = GeneratePairingCode();
                _lastPairingCode = code;

                // Emitir evento de código de pairing
                PairingCodeReceived?.Invoke(this, code);

                // Auto-completar después de 5 segundos
                await Task.Delay(5000);
                if (State == ConnectionState.PairingRequired)
                {
                    Log.Warning("Auto-completing pairing (fallback mode)");
                    var authToken = GenerateAuthToken();
                    
                    // Guardar token
                    config.Clover.AuthToken = authToken;
                    _configService.UpdateConfig(config);
                    
                    UpdateState(ConnectionState.Paired);
                }
            }
        });

        Log.Information("Pairing request sent successfully - waiting for terminal response");
    }

    private string GeneratePairingCode()
    {
        // Generar código de 6 dígitos como en TypeScript
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private string GenerateAuthToken()
    {
        // Generar token aleatorio
        return Guid.NewGuid().ToString("N");
    }

    public async Task SendMessageAsync(CloverMessage message)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            Log.Error("❌ SEND FAILED - WebSocket not connected");
            throw new InvalidOperationException("WebSocket is not connected");
        }

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        // Log detallado del mensaje enviado
        Log.Debug("═══════════════════════════════════════════════════════════════════");
        Log.Debug("📤 ENVIANDO AL TERMINAL");
        Log.Debug("───────────────────────────────────────────────────────────────────");
        Log.Debug("  Método: {Method}", message.Method);
        Log.Debug("  ID: {Id}", message.Id ?? "N/A");
        Log.Debug("  Tamaño: {Size} bytes", bytes.Length);
        Log.Debug("  JSON: {Json}", json.Length > 1000 ? json.Substring(0, 1000) + "..." : json);
        Log.Debug("═══════════════════════════════════════════════════════════════════");
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None
        );

        Log.Information("📤 Mensaje enviado: Method={Method} ID={Id} Size={Size}bytes", 
            message.Method, message.Id ?? "N/A", bytes.Length);
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null) return;

        var buffer = new byte[1024 * 16];
        var messageBuilder = new StringBuilder();

        while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log.Information("WebSocket closed by server");
                    break;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(text);

                if (result.EndOfMessage)
                {
                    var fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    Log.Debug("Received: {Message}", fullMessage);
                    await HandleMessageAsync(fullMessage);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error receiving message");
                break;
            }
        }
    }

    private async Task HandleMessageAsync(string messageJson)
    {
        try
        {
            // Log detallado del mensaje recibido
            Log.Debug("═══════════════════════════════════════════════════════════════════");
            Log.Debug("📥 RECIBIDO DEL TERMINAL");
            Log.Debug("───────────────────────────────────────────────────────────────────");
            Log.Debug("  Tamaño: {Size} bytes", messageJson.Length);
            Log.Debug("  JSON completo:");
            
            // Dividir JSON largo en líneas para mejor legibilidad
            if (messageJson.Length > 500)
            {
                // Intentar formatear el JSON para que sea legible
                try
                {
                    var jsonDoc = JsonDocument.Parse(messageJson);
                    var formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    foreach (var line in formattedJson.Split('\n').Take(50))
                    {
                        Log.Debug("    {Line}", line);
                    }
                    if (formattedJson.Split('\n').Length > 50)
                        Log.Debug("    ... (truncado)");
                }
                catch
                {
                    Log.Debug("    {Json}", messageJson);
                }
            }
            else
            {
                Log.Debug("    {Json}", messageJson);
            }
            Log.Debug("═══════════════════════════════════════════════════════════════════");
            
            var message = JsonSerializer.Deserialize<CloverMessage>(messageJson, JsonOptions);
            if (message == null)
            {
                Log.Warning("⚠️ Failed to deserialize message");
                Log.Warning("  Raw JSON: {Json}", messageJson);
                return;
            }

            Log.Information("📥 Mensaje recibido: Method={Method}, ID={Id}, Size={Size}bytes", 
                message.Method, message.Id ?? "N/A", messageJson.Length);


            switch (message.Method)
            {
                case "PAIRING_CODE":
                    Log.Information("🔑 Processing PAIRING_CODE message");
                    await HandlePairingCodeAsync(message);
                    break;

                case "PAIRING_RESPONSE":
                    Log.Information("🔐 Processing PAIRING_RESPONSE message");
                    await HandlePairingResponseAsync(message);
                    break;

                case "ACK":
                    Log.Debug("✅ Received ACK for message {Id}", message.Id);
                    break;

                case "FINISH_OK":
                    Log.Information("✅ PAGO EXITOSO - FINISH_OK recibido");
                    LogPaymentDetails(message, true);
                    HandleTransactionResponse(message); // Resuelve el pending con éxito
                    break;

                case "FINISH_CANCEL":
                    Log.Warning("❌ PAGO CANCELADO - FINISH_CANCEL recibido");
                    LogPaymentDetails(message, false);
                    HandleTransactionResponse(message); // Resuelve el pending con cancelación
                    break;

                case "TX_START_RESPONSE":
                    // TX_START_RESPONSE = Terminal recibió la solicitud y está procesando
                    // NO resuelve el pending, solo es informativo
                    Log.Information("📝 TX_START_RESPONSE - Transacción en proceso (esperando FINISH_OK o FINISH_CANCEL)");
                    // No llamar HandleTransactionResponse - esperar FINISH_OK o FINISH_CANCEL
                    break;

                case "REFUND_RESPONSE":
                case "VOID_PAYMENT_RESPONSE":
                    Log.Information("💳 Processing transaction response: {Method}", message.Method);
                    HandleTransactionResponse(message);
                    break;

                case "CONFIRM_PAYMENT":
                case "PARTIAL_AUTH":
                case "TIP_ADJUST":
                    Log.Information("💵 Processing payment event: {Method}", message.Method);
                    MessageReceived?.Invoke(this, message);
                    break;

                case "UI_STATE":
                    // UI State es informativo, loguear solo en debug
                    Log.Debug("📺 UI_STATE: {Payload}", message.Payload?.ToString()?.Substring(0, Math.Min(100, message.Payload?.ToString()?.Length ?? 0)));
                    break;

                default:
                    Log.Debug("📨 Mensaje no manejado: {Method}", message.Method);
                    MessageReceived?.Invoke(this, message);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling message");
        }
    }

    /// <summary>
    /// Log detallado de los datos del pago recibidos del terminal
    /// </summary>
    private void LogPaymentDetails(CloverMessage message, bool? isSuccess)
    {
        try
        {
            if (message.Payload == null)
            {
                Log.Warning("  ⚠️ No hay payload en el mensaje");
                return;
            }

            // Serializar payload para parsearlo
            var payloadJson = JsonSerializer.Serialize(message.Payload);
            var payload = JsonDocument.Parse(payloadJson).RootElement;
            
            Log.Information("═══════════════════════════════════════════════════════════════════");
            Log.Information("  📋 DETALLES DEL PAGO - {Method}", message.Method);
            Log.Information("───────────────────────────────────────────────────────────────────");
            
            // El objeto payment puede estar stringificado
            if (payload.TryGetProperty("payment", out var paymentProp))
            {
                JsonElement payment;
                
                // Checar si es string (stringified) o objeto directo
                if (paymentProp.ValueKind == JsonValueKind.String)
                {
                    var paymentStr = paymentProp.GetString();
                    if (!string.IsNullOrEmpty(paymentStr))
                    {
                        payment = JsonDocument.Parse(paymentStr).RootElement;
                    }
                    else
                    {
                        Log.Warning("  ⚠️ Payment string está vacío");
                        return;
                    }
                }
                else
                {
                    payment = paymentProp;
                }
                
                // Datos principales del pago
                var id = payment.TryGetProperty("id", out var idProp) ? idProp.GetString() : "N/A";
                var externalId = payment.TryGetProperty("externalPaymentId", out var extIdProp) ? extIdProp.GetString() : "N/A";
                var amount = payment.TryGetProperty("amount", out var amountProp) ? amountProp.GetInt64() : 0;
                var result = payment.TryGetProperty("result", out var resultProp) ? resultProp.GetString() : "N/A";
                
                Log.Information("  💰 RESULTADO: {Result}", result);
                Log.Information("  🆔 ID Transacción: {Id}", id);
                Log.Information("  📄 External ID: {ExtId}", externalId);
                Log.Information("  💵 Monto: ${Amount} ({AmountCents} centavos)", amount / 100m, amount);
                
                // Order ID
                if (payment.TryGetProperty("order", out var orderProp) && 
                    orderProp.TryGetProperty("id", out var orderIdProp))
                {
                    Log.Information("  📦 Order ID: {OrderId}", orderIdProp.GetString());
                }
                
                // Card Transaction (authCode, cardType)
                if (payment.TryGetProperty("cardTransaction", out var cardTx))
                {
                    var authCode = cardTx.TryGetProperty("authCode", out var authProp) ? authProp.GetString() : "N/A";
                    var cardType = cardTx.TryGetProperty("cardType", out var typeProp) ? typeProp.GetString() : "N/A";
                    var currency = cardTx.TryGetProperty("currency", out var currProp) ? currProp.GetString() : "N/A";
                    
                    Log.Information("  🔐 Código Autorización (Cupón): {AuthCode}", authCode);
                    Log.Information("  💳 Tipo de Tarjeta: {CardType}", cardType);
                    Log.Information("  💱 Moneda: {Currency}", currency?.ToUpper());
                }
                
                // Transaction Info (mejor descripcion de tipo de pago)
                if (payment.TryGetProperty("transactionInfo", out var txInfo))
                {
                    var cardTypeLabel = txInfo.TryGetProperty("cardTypeLabel", out var labelProp) ? labelProp.GetString() : null;
                    var entryType = txInfo.TryGetProperty("entryType", out var entryProp) ? entryProp.GetString() : null;
                    
                    if (!string.IsNullOrEmpty(cardTypeLabel))
                        Log.Information("  🏷️ Método de Pago: {Label}", cardTypeLabel);
                    if (!string.IsNullOrEmpty(entryType))
                        Log.Information("  📲 Tipo de Entrada: {EntryType}", entryType);
                }
                
                // Note (contiene info adicional de QR, Billetera, etc.)
                if (payment.TryGetProperty("note", out var noteProp))
                {
                    var note = noteProp.GetString();
                    if (!string.IsNullOrEmpty(note))
                    {
                        Log.Information("  📝 Nota del Pago:");
                        // Parsear la nota que viene con formato "clave: valor;"
                        foreach (var part in note.Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            Log.Information("      - {NotePart}", part.Trim());
                        }
                    }
                }
                
                // Tender (tipo de medio de pago)
                if (payment.TryGetProperty("tender", out var tenderProp))
                {
                    var labelKey = tenderProp.TryGetProperty("labelKey", out var lkProp) ? lkProp.GetString() : null;
                    if (!string.IsNullOrEmpty(labelKey))
                        Log.Information("  🎫 Tender: {LabelKey}", labelKey);
                }
                
                // Device ID
                if (payment.TryGetProperty("device", out var deviceProp) && 
                    deviceProp.TryGetProperty("id", out var deviceIdProp))
                {
                    Log.Information("  📱 Device ID: {DeviceId}", deviceIdProp.GetString());
                }
                
                // Created Time
                if (payment.TryGetProperty("createdTime", out var createdTimeProp))
                {
                    try
                    {
                        var timestamp = createdTimeProp.GetInt64();
                        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                        Log.Information("  🕐 Fecha/Hora: {DateTime}", dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    catch { }
                }
            }
            else
            {
                // Si no hay payment object, mostrar payload completo
                Log.Information("  📄 Payload (sin objeto payment):");
                Log.Information("      {Payload}", payloadJson.Length > 500 ? payloadJson.Substring(0, 500) + "..." : payloadJson);
            }
            
            Log.Information("═══════════════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error al parsear detalles del pago");
        }
    }

    private void HandleTransactionResponse(CloverMessage message)
    {
        try
        {
            Log.Information("🔍 DEBUG: Handling transaction response - Method: {Method}, Message ID: {MessageId}, Payload type: {PayloadType}", 
                message.Method, message.Id, message.Payload?.GetType().Name ?? "null");

            // El terminal envía su PROPIO ID en la respuesta, no el ID que recibió
            // Usar el ID del mensaje de respuesta del terminal
            var responseId = message.Id;
            
            if (string.IsNullOrEmpty(responseId))
            {
                Log.Warning("🔍 DEBUG: No ID in response message");
                MessageReceived?.Invoke(this, message);
                return;
            }

            // Si hay un handler para este ID de respuesta, resolver la TaskCompletionSource
            if (_pendingMessages.TryGetValue(responseId, out var tcs))
            {
                Log.Information("✅ Completing pending transaction {Id}", responseId);
                _pendingMessages.Remove(responseId);
                tcs.SetResult(message);
                return;
            }

            // Si no hay handler exacto, maybe el terminal usa IDs diferentes
            // Intentar encontrar si el payload contiene información útil
            if (message.Payload != null)
            {
                Dictionary<string, JsonElement>? payloadObj = null;

                if (message.Payload is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var payloadStr = element.GetString();
                        if (!string.IsNullOrEmpty(payloadStr))
                        {
                            payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadStr);
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        var payloadJson = element.GetRawText();
                        payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
                    }
                }

                if (payloadObj != null && payloadObj.TryGetValue("id", out var idElement))
                {
                    var payloadId = idElement.ValueKind == JsonValueKind.Number 
                        ? idElement.GetInt32().ToString()
                        : idElement.GetString();
                    
                    if (!string.IsNullOrEmpty(payloadId) && payloadId != responseId && _pendingMessages.TryGetValue(payloadId, out var payloadTcs))
                    {
                        Log.Information("✅ Completing pending transaction using payload ID {Id}", payloadId);
                        _pendingMessages.Remove(payloadId);
                        payloadTcs.SetResult(message);
                        return;
                    }
                }
            }

            // Si no encontramos handler, notificar como mensaje normal o intentar resolver el último
            if (_pendingMessages.Count > 0)
            {
                Log.Information("📨 No matching handler for ID {Id} but have {Count} pending - using last pending", 
                    responseId, _pendingMessages.Count);
                
                // Resolver el primer handler pendiente (asumiendo que es el más reciente)
                var firstPending = _pendingMessages.First();
                _pendingMessages.Remove(firstPending.Key);
                firstPending.Value.SetResult(message);
                return;
            }

            Log.Information("📨 Transaction response {Id} without any pending handlers - notifying subscribers", responseId);
            MessageReceived?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling transaction response");
        }
    }

    private Task HandlePairingCodeAsync(CloverMessage message)
    {
        try
        {
            Log.Information("🔢 Received PAIRING_CODE from terminal");
            
            // Extraer código del payload - puede venir como string o como objeto
            string? code = null;
            
            if (message.Payload != null)
            {
                Dictionary<string, JsonElement>? payloadObj = null;
                
                if (message.Payload is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var payloadStr = element.GetString();
                        if (!string.IsNullOrEmpty(payloadStr))
                        {
                            payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadStr);
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        var payloadJson = element.GetRawText();
                        payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
                    }
                }
                else if (message.Payload is string payloadString)
                {
                    payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadString);
                }
                
                // Buscar pairingCode o code
                if (payloadObj != null)
                {
                    if (payloadObj.TryGetValue("pairingCode", out var codeElement))
                    {
                        code = codeElement.GetString();
                    }
                    else if (payloadObj.TryGetValue("code", out var codeElement2))
                    {
                        code = codeElement2.GetString();
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(code))
            {
                _lastPairingCode = code;
                Log.Information("🔢 PAIRING CODE: {Code}", code);
                Console.WriteLine($"🔢 PAIRING CODE: {code}");
                
                // Emitir evento
                PairingCodeReceived?.Invoke(this, code);
            }
            else
            {
                Log.Warning("⚠️ PAIRING_CODE message received but no code found in payload");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling pairing code");
        }
        
        return Task.CompletedTask;
    }

    private Task HandlePairingResponseAsync(CloverMessage message)
    {
        try
        {
            Log.Information("📨 Received PAIRING_RESPONSE from terminal");
            
            // Extraer datos del payload
            Dictionary<string, JsonElement>? payloadObj = null;
            
            if (message.Payload != null)
            {
                if (message.Payload is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var payloadStr = element.GetString();
                        if (!string.IsNullOrEmpty(payloadStr))
                        {
                            payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadStr);
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        var payloadJson = element.GetRawText();
                        payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
                    }
                }
                else if (message.Payload is string payloadString)
                {
                    payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadString);
                }
            }
            
            if (payloadObj != null)
            {
                // Verificar pairingState
                string? pairingState = null;
                if (payloadObj.TryGetValue("pairingState", out var stateElement))
                {
                    pairingState = stateElement.GetString();
                    Log.Information("📊 Pairing state: {State}", pairingState);
                }
                
                // Buscar authToken
                if (payloadObj.TryGetValue("authenticationToken", out var tokenElement))
                {
                    var authToken = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        Log.Information("✅ Received auth token from pairing (***{Last4})", authToken.Substring(Math.Max(0, authToken.Length - 4)));
                        
                        // Guardar el token
                        var config = _configService.GetConfig();
                        config.Clover.AuthToken = authToken;
                        _configService.UpdateConfig(config);
                    }
                }
                
                // Cambiar estado solo si pairing exitoso
                if (pairingState == "PAIRED" || pairingState == "INITIAL")
                {
                    Log.Information("✅ Pairing completed successfully!");
                    Console.WriteLine("✅ Pairing completado exitosamente!");
                    UpdateState(ConnectionState.Paired);
                    _lastPairingCode = null;
                }
                else if (pairingState == "FAILED")
                {
                    Log.Error("❌ Pairing failed - terminal rejected pairing");
                    Console.WriteLine("❌ Pairing rechazado por el terminal");
                    UpdateState(ConnectionState.Error);
                }
                else if (pairingState == "AUTHENTICATING")
                {
                    Log.Information("⏳ Waiting for manager PIN on terminal");
                    Console.WriteLine("⏳ Esperando PIN de gerente en el terminal");
                }
                else
                {
                    Log.Warning("⚠️ Unknown pairing state: {State}", pairingState ?? "null");
                }
            }
            else
            {
                Log.Warning("⚠️ PAIRING_RESPONSE received but no payload found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling pairing response");
        }
        
        return Task.CompletedTask;
    }

    // Métodos para enviar transacciones (copiados del sistema TypeScript)
    private int _messageId = 0;
    private readonly Dictionary<string, TaskCompletionSource<CloverMessage>> _pendingMessages = new();

    public async Task<CloverMessage> SendSaleAsync(decimal amount, string externalId, decimal tipAmount = 0)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        if (State != ConnectionState.Paired)
        {
            throw new InvalidOperationException("Must be paired to send transactions");
        }

        UpdateState(ConnectionState.Busy);

        try
        {
            var config = _configService.GetConfig();
            var id = (++_messageId).ToString();

            // Crear PayIntent según protocolo Clover (igual que TypeScript)
            var payIntent = new
            {
                action = "com.clover.intent.action.PAY",
                amount = (long)(amount * 100), // Convertir a centavos
                tipAmount = (long)(tipAmount * 100),
                taxAmount = 0,
                orderId = (string?)null,
                paymentId = (string?)null,
                employeeId = (string?)null,
                transactionType = "PAYMENT",
                taxableAmountRateList = (object?)null,
                isDisableCashBack = false,
                isTesting = false,
                voiceAuthCode = (string?)null,
                postalCode = (string?)null,
                streetAddress = (string?)null,
                isCardNotPresent = false,
                cardNotPresent = false,
                transactionNo = (string?)null,
                isForceSwipePinEntry = false,
                disableRestartTransactionWhenFailed = false,
                externalPaymentId = externalId,
                externalReferenceId = (string?)null,
                vaultedCard = (object?)null,
                allowOfflinePayment = true,
                approveOfflinePaymentWithoutPrompt = true,
                requiresRemoteConfirmation = true,
                applicationTracking = (object?)null,
                allowPartialAuth = false,
                germanInfo = (object?)null,
                remotePrint = false,
                transactionSettings = new
                {
                    cloverShouldHandleReceipts = false,
                    disableCashBack = false,
                    forcePinEntryOnSwipe = false,
                    disableRestartTransactionOnFailure = false,
                    allowOfflinePayment = true,
                    approveOfflinePaymentWithoutPrompt = true,
                    forceOfflinePayment = false,
                    signatureThreshold = (long?)null,
                    signatureEntryLocation = (string?)null,
                    tipMode = "NO_TIP",
                    tippableAmount = (long?)null,
                    disableReceiptSelection = false,
                    disableDuplicateCheck = false,
                    autoAcceptPaymentConfirmations = true,
                    autoAcceptSignature = true,
                    regionalExtras = (object?)null,
                    cardEntryMethods = (int?)null,
                    tipSuggestions = (object?)null
                }
            };

            // Crear el payload interno con el método y datos
            var innerPayload = new
            {
                id,
                method = "TX_START",
                payIntent,
                order = (object?)null,
                requestInfo = "SALE"
            };

            // Envolver en RemoteMessage como espera Clover (igual que TypeScript)
            var remoteMessage = new
            {
                method = "TX_START",
                payload = JsonSerializer.Serialize(innerPayload), // Stringify payload!
                remoteApplicationID = config.Clover.RemoteAppId ?? "com.mycompany.cloverbridge",
                remoteSourceSDK = "CloverBridge-1.0.0",
                version = 2, // Version 2 para transacciones
                directed = true, // Mensaje dirigido
                packageName = "com.clover.remote_protocol_broadcast.app"
            };

            var json = JsonSerializer.Serialize(remoteMessage);
            
            // Log detallado de la transacción
            Log.Debug("═══════════════════════════════════════════════════════════════════");
            Log.Debug("💳 ENVIANDO TRANSACCION SALE AL TERMINAL");
            Log.Debug("───────────────────────────────────────────────────────────────────");
            Log.Debug("  ID de transacción: {Id}", id);
            Log.Debug("  Monto: ${Amount} ({AmountCents} centavos)", amount / 100m, amount);
            Log.Debug("  ExternalPaymentId: {ExtId}", externalId);
            Log.Debug("  Tip: ${Tip}", tipAmount / 100m);
            Log.Debug("  RemoteAppId: {AppId}", config.Clover.RemoteAppId);
            Log.Debug("───────────────────────────────────────────────────────────────────");
            Log.Debug("  JSON completo ({Size} bytes):", json.Length);
            // Formatear JSON para legibilidad
            try
            {
                var formattedPayIntent = JsonSerializer.Serialize(payIntent, new JsonSerializerOptions { WriteIndented = true });
                foreach (var line in formattedPayIntent.Split('\n').Take(30))
                {
                    Log.Debug("    {Line}", line);
                }
            }
            catch { Log.Debug("    {Json}", json); }
            Log.Debug("═══════════════════════════════════════════════════════════════════");
            
            Log.Information("💳 Enviando SALE: ID={Id} Monto=${Amount} ExternalId={ExtId}", 
                id, amount / 100m, externalId);

            // Crear TaskCompletionSource para esperar respuesta
            var tcs = new TaskCompletionSource<CloverMessage>();
            _pendingMessages[id] = tcs;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None
            );

            // Esperar respuesta con timeout de 120 segundos
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            cts.Token.Register(() => 
            {
                Log.Warning("⏱️ Transaction timeout after 120 seconds for ID {Id}", id);
                tcs.TrySetException(new TimeoutException("Transaction timed out after 120 seconds"));
            });

            var response = await tcs.Task;
            UpdateState(ConnectionState.Paired);
            return response;
        }
        catch
        {
            UpdateState(ConnectionState.Paired);
            throw;
        }
    }

    public async Task<CloverMessage> SendRefundAsync(decimal amount, string? paymentId, string? orderId, bool fullRefund = false)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        if (State != ConnectionState.Paired)
        {
            throw new InvalidOperationException("Must be paired to send transactions");
        }

        UpdateState(ConnectionState.Busy);

        try
        {
            var config = _configService.GetConfig();
            var id = (++_messageId).ToString();

            // Crear el payload interno
            var innerPayload = new
            {
                id,
                method = "REFUND",
                amount = (long)(amount * 100), // Convertir a centavos
                orderId,
                paymentId,
                fullRefund
            };

            // Envolver en RemoteMessage
            var remoteMessage = new
            {
                method = "REFUND",
                payload = JsonSerializer.Serialize(innerPayload),
                remoteApplicationID = config.Clover.RemoteAppId ?? "com.mycompany.cloverbridge",
                remoteSourceSDK = "CloverBridge-1.0.0",
                version = 2,
                directed = true,
                packageName = "com.clover.remote_protocol_broadcast.app"
            };

            var json = JsonSerializer.Serialize(remoteMessage);
            Log.Information("📤 Sending REFUND transaction: {Json}", json);

            // Crear TaskCompletionSource para esperar respuesta
            var tcs = new TaskCompletionSource<CloverMessage>();
            _pendingMessages[id] = tcs;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None
            );

            // Esperar respuesta con timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            cts.Token.Register(() => 
            {
                Log.Warning("⏱️ Refund timeout after 120 seconds for ID {Id}", id);
                tcs.TrySetException(new TimeoutException("Refund timed out after 120 seconds"));
            });

            var response = await tcs.Task;
            UpdateState(ConnectionState.Paired);
            return response;
        }
        catch
        {
            UpdateState(ConnectionState.Paired);
            throw;
        }
    }

    /// <summary>
    /// Cancela una transacción en curso en el terminal
    /// </summary>
    public async Task<CloverMessage> CancelTransactionAsync()
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        try
        {
            var config = _configService.GetConfig();
            var id = (++_messageId).ToString();

            // Crear el mensaje de cancelación
            var innerPayload = new
            {
                id,
                method = "BREAK"
            };

            // Envolver en RemoteMessage
            var remoteMessage = new
            {
                method = "BREAK",
                payload = JsonSerializer.Serialize(innerPayload),
                remoteApplicationID = config.Clover.RemoteAppId ?? "com.mycompany.cloverbridge",
                remoteSourceSDK = "CloverBridge-1.0.0",
                version = 2,
                directed = true,
                packageName = "com.clover.remote_protocol_broadcast.app"
            };

            var json = JsonSerializer.Serialize(remoteMessage);
            Log.Information("📤 Sending CANCEL transaction (BREAK): {Json}", json);

            // Crear TaskCompletionSource para esperar respuesta
            var tcs = new TaskCompletionSource<CloverMessage>();
            _pendingMessages[id] = tcs;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None
            );

            // Esperar respuesta con timeout de 30 segundos
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => 
            {
                Log.Warning("⏱️ Cancel timeout after 30 seconds");
                tcs.TrySetException(new TimeoutException("Cancel timed out after 30 seconds"));
            });

            var response = await tcs.Task;
            return response;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error cancelling transaction, terminal may not support BREAK");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing WebSocket");
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        UpdateState(ConnectionState.Disconnected);
    }

    private void UpdateState(ConnectionState newState)
    {
        if (_state != newState)
        {
            _state = newState;
            Log.Information("Connection state changed to {State}", newState);
            StateChanged?.Invoke(this, newState);
        }
    }
}
