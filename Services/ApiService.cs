using CloverBridge.Models;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CloverBridge.Services;

/// <summary>
/// API HTTP para comunicación con clientes externos
/// </summary>
public class ApiService : BackgroundService
{
    private readonly ConfigurationService _configService;
    private readonly CloverWebSocketService _cloverService;
    private readonly TransactionQueueService _queueService;
    private HttpListener? _listener;

    public ApiService(
        ConfigurationService configService,
        CloverWebSocketService cloverService,
        TransactionQueueService queueService)
    {
        _configService = configService;
        _cloverService = cloverService;
        _queueService = queueService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configService.GetConfig();
        var url = $"http://{config.Api.Host}:{config.Api.Port}/";

        _listener = new HttpListener();
        _listener.Prefixes.Add(url);

        int retries = 0;
        const int maxRetries = 3;

        while (retries < maxRetries && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                _listener.Start();
                Log.Information("API Server started on {Url}", url);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = HandleRequestAsync(context, stoppingToken);
                    }
                    catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                    {
                        // Listener stopped
                        break;
                    }
                }
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 183) // Address already in use
            {
                retries++;
                Log.Warning("Port {Port} is already in use (attempt {Retry}/{Max}). Retrying in 2 seconds...", 
                    config.Api.Port, retries, maxRetries);
                
                if (retries < maxRetries)
                {
                    await Task.Delay(2000, stoppingToken);
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(url);
                }
                else
                {
                    Log.Error("Failed to start API server after {Max} retries. Port {Port} is in use.", 
                        maxRetries, config.Api.Port);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting API server");
                break;
            }
        }

        try
        {
            _listener?.Stop();
        }
        catch { }
        
        Log.Information("API Server stopped");
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // CORS headers
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            Log.Information("API Request: {Method} {Path}", request.HttpMethod, path);

            object? result = path switch
            {
                "/api/health" => await HandleHealthAsync(),
                "/api/status" => await HandleStatusAsync(),
                "/api/connect" when request.HttpMethod == "POST" => await HandleConnectAsync(),
                "/api/disconnect" when request.HttpMethod == "POST" => await HandleDisconnectAsync(),
                "/api/config" when request.HttpMethod == "GET" => await HandleGetConfigAsync(),
                "/api/config" when request.HttpMethod == "POST" => await HandleUpdateConfigAsync(request),
                "/api/transaction/sale" when request.HttpMethod == "POST" => await HandleSaleAsync(request),
                "/api/transaction/void" when request.HttpMethod == "POST" => await HandleVoidAsync(request),
                "/api/transaction/refund" when request.HttpMethod == "POST" => await HandleRefundAsync(request),
                "/api/qr" when request.HttpMethod == "POST" => await HandleQrAsync(request),
                _ => new { error = "Not found" }
            };

            if (result == null)
            {
                response.StatusCode = 404;
                result = new { error = "Not found" };
            }
            else if (path == "/" || path == "/api/health")
            {
                response.StatusCode = 200;
            }

            var json = JsonSerializer.Serialize(result);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken);
            response.Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling request");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
        }
    }

    private Task<object> HandleHealthAsync()
    {
        return Task.FromResult<object>(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow
        });
    }

    private Task<object> HandleStatusAsync()
    {
        var config = _configService.GetConfig();
        
        return Task.FromResult<object>(new
        {
            clover = new
            {
                state = _cloverService.State.ToString(),
                endpoint = config.Clover.GetWebSocketUrl(),
                lastPairingCode = _cloverService.LastPairingCode
            },
            queue = _queueService.GetStatus(),
            configuration = new
            {
                cloverHost = config.Clover.Host,
                cloverPort = config.Clover.Port,
                remoteAppId = config.Clover.RemoteAppId,
                apiPort = config.Api.Port,
                folders = config.Folders,
                transaction = config.Transaction
            }
        });
    }

    private async Task<object> HandleConnectAsync()
    {
        // La conexión se maneja automáticamente
        return new { success = true, message = "Connection managed automatically" };
    }

    private async Task<object> HandleDisconnectAsync()
    {
        await _cloverService.DisconnectAsync();
        return new { success = true };
    }

    private Task<object> HandleGetConfigAsync()
    {
        var config = _configService.GetConfig();
        return Task.FromResult<object>(config);
    }

    private async Task<object> HandleUpdateConfigAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var newConfig = JsonSerializer.Deserialize<AppConfig>(body);

        if (newConfig != null)
        {
            _configService.UpdateConfig(newConfig);
            return new { success = true };
        }

        return new { success = false, error = "Invalid configuration" };
    }

    private async Task<object> HandleSaleAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        
        // Deserializar el request
        var requestData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        if (requestData == null)
            return new { success = false, error = "Invalid request" };

        // Extraer los parámetros
        if (!requestData.TryGetValue("amount", out var amountElement) || 
            !requestData.TryGetValue("externalId", out var externalIdElement))
        {
            return new { success = false, error = "Missing required fields: amount, externalId" };
        }

        var amount = amountElement.GetDecimal();
        var externalId = externalIdElement.GetString() ?? "";
        var tipAmount = 0m;
        
        if (requestData.TryGetValue("tipAmount", out var tipElement) && tipElement.ValueKind != JsonValueKind.Null)
        {
            tipAmount = tipElement.GetDecimal();
        }

        try
        {
            // Llamar directamente al método SendSaleAsync
            var response = await _cloverService.SendSaleAsync(amount, externalId, tipAmount);
            return new
            {
                success = true,
                message = "Sale transaction completed",
                response = response
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing sale transaction");
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> HandleVoidAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var voidRequest = JsonSerializer.Deserialize<Dictionary<string, object>>(body);

        if (voidRequest == null)
            return new { success = false, error = "Invalid request" };

        var message = new CloverMessage
        {
            Method = "VOID_PAYMENT",
            Id = Guid.NewGuid().ToString(),
            Payload = voidRequest
        };

        var response = await _queueService.EnqueueTransactionAsync(message);
        return response;
    }

    private async Task<object> HandleRefundAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var requestData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

        if (requestData == null)
            return new { success = false, error = "Invalid request" };

        if (!requestData.TryGetValue("amount", out var amountElement))
            return new { success = false, error = "Missing required field: amount" };

        var amount = amountElement.GetDecimal();
        var paymentId = requestData.TryGetValue("paymentId", out var pidElement) 
            ? pidElement.GetString() 
            : null;
        var orderId = requestData.TryGetValue("orderId", out var oidElement) 
            ? oidElement.GetString() 
            : null;
        var fullRefund = requestData.TryGetValue("fullRefund", out var frElement) && frElement.GetBoolean();

        try
        {
            var response = await _cloverService.SendRefundAsync(amount, paymentId, orderId, fullRefund);
            return new
            {
                success = true,
                message = "Refund transaction completed",
                response = response
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing refund transaction");
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> HandleQrAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var qrRequest = JsonSerializer.Deserialize<QrCodeRequest>(body);

        if (qrRequest == null)
            return new { success = false, error = "Invalid request" };

        var message = new CloverMessage
        {
            Method = "SHOW_DISPLAY_ORDER",
            Id = Guid.NewGuid().ToString(),
            Payload = qrRequest
        };

        var response = await _queueService.EnqueueTransactionAsync(message);
        return response;
    }

    public override void Dispose()
    {
        _listener?.Stop();
        _listener?.Close();
        base.Dispose();
    }
}
