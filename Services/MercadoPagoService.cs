using CloverBridge.Models;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CloverBridge.Services;

public class MercadoPagoService
{
    private readonly ConfigurationService _configService;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, string> _posToInvoiceMap = new();
    private readonly ILogger _logger = Log.ForContext<MercadoPagoService>();

    public MercadoPagoService(ConfigurationService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
    }

    private void SetHeaders(string idempotencyKey = null)
    {
        var config = _configService.GetConfig();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Qrmp.AccessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Idempotency-Key", idempotencyKey);
        }
    }

    public async Task<bool> CreateOrderAsync(TransactionFile transaction)
    {
        var config = _configService.GetConfig();
        var url = $"https://api.mercadopago.com/instore/qr/seller/collectors/{config.Qrmp.UserId}/stores/{config.Qrmp.ExternalStoreId}/pos/{config.Qrmp.ExternalPosId}/orders";

        var idempotencyKey = Guid.NewGuid().ToString();
        SetHeaders(idempotencyKey);

        var orderRequest = new
        {
            external_reference = transaction.InvoiceNumber,
            description = transaction.Notes ?? $"Venta {transaction.InvoiceNumber}",
            total_amount = transaction.Amount,
            items = new[]
            {
                new
                {
                    title = $"Factura {transaction.InvoiceNumber}",
                    quantity = 1,
                    unit_price = transaction.Amount,
                    total_amount = transaction.Amount
                }
            },
            notification_url = config.Qrmp.WebhookUrl,
            expiration_date = DateTime.UtcNow.AddSeconds(config.Qrmp.OrderTtlSeconds).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        var json = JsonSerializer.Serialize(orderRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            _logger.Information("MP: Creating order for Invoice={Invoice} at POS={POS}", transaction.InvoiceNumber, config.Qrmp.ExternalPosId);
            var response = await _httpClient.PutAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.Information("MP: Order created successfully (204 No Content)");
                _posToInvoiceMap[config.Qrmp.ExternalPosId] = transaction.InvoiceNumber;
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.Error("MP: Error creating order. Status={Status}, Body={Body}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MP: Exception creating order");
            return false;
        }
    }

    public async Task<bool> DeleteOrderAsync()
    {
        var config = _configService.GetConfig();
        var url = $"https://api.mercadopago.com/instore/qr/seller/collectors/{config.Qrmp.UserId}/pos/{config.Qrmp.ExternalPosId}/orders";

        SetHeaders(Guid.NewGuid().ToString());

        try
        {
            var response = await _httpClient.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MP: Error deleting order");
            return false;
        }
    }

    public async Task<JsonElement?> GetPaymentAsync(string paymentId)
    {
        var url = $"https://api.mercadopago.com/v1/payments/{paymentId}";
        SetHeaders();

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(body);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MP: Error getting payment {PaymentId}", paymentId);
            return null;
        }
    }

    public async Task<bool> TestCredentialsAsync()
    {
        var config = _configService.GetConfig();
        var url = $"https://api.mercadopago.com/users/{config.Qrmp.UserId}/stores/search?external_id={config.Qrmp.ExternalStoreId}";
        SetHeaders();

        try
        {
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
