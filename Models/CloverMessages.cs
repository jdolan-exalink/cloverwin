using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloverBridge.Models;

/// <summary>
/// Mensaje base de Clover
/// </summary>
public class CloverMessage
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("version")]
    [JsonConverter(typeof(JsonElementConverter))]
    public object? Version { get; set; }

    [JsonPropertyName("payload")]
    [JsonConverter(typeof(JsonElementConverter))]
    public object? Payload { get; set; }
    
    [JsonPropertyName("directed")]
    public bool? Directed { get; set; }
    
    [JsonPropertyName("packageName")]
    public string? PackageName { get; set; }
    
    [JsonPropertyName("remoteApplicationID")]
    public string? RemoteApplicationID { get; set; }
    
    [JsonPropertyName("remoteSourceSDK")]
    public string? RemoteSourceSDK { get; set; }
    
    [JsonPropertyName("remotePayCompatibilityVersion")]
    public int? RemotePayCompatibilityVersion { get; set; }
}

/// <summary>
/// Solicitud de pairing
/// </summary>
public class PairingRequest
{
    [JsonPropertyName("remoteApplicationID")]
    public string RemoteApplicationId { get; set; } = string.Empty;

    [JsonPropertyName("posName")]
    public string PosName { get; set; } = string.Empty;

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta de pairing
/// </summary>
public class PairingResponse
{
    [JsonPropertyName("pairingCode")]
    public string? PairingCode { get; set; }

    [JsonPropertyName("authenticationToken")]
    public string? AuthenticationToken { get; set; }
}

/// <summary>
/// Solicitud de transacción (SALE)
/// </summary>
public class SaleRequest
{
    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "SALE";

    [JsonPropertyName("tipMode")]
    public string? TipMode { get; set; }

    [JsonPropertyName("signatureEntryLocation")]
    public string? SignatureEntryLocation { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Solicitud de QR Code
/// </summary>
public class QrCodeRequest
{
    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta de transacción
/// </summary>
public class TransactionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("payment")]
    public PaymentInfo? Payment { get; set; }

    [JsonPropertyName("externalPaymentId")]
    public string? ExternalPaymentId { get; set; }
}

public class PaymentInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public OrderInfo? Order { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("tipAmount")]
    public long? TipAmount { get; set; }

    [JsonPropertyName("externalPaymentId")]
    public string? ExternalPaymentId { get; set; }

    [JsonPropertyName("cardTransaction")]
    public CardTransaction? CardTransaction { get; set; }
}

public class OrderInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class CardTransaction
{
    [JsonPropertyName("cardType")]
    public string? CardType { get; set; }

    [JsonPropertyName("last4")]
    public string? Last4 { get; set; }

    [JsonPropertyName("first6")]
    public string? First6 { get; set; }

    [JsonPropertyName("authCode")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("referenceId")]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("transactionNo")]
    public string? TransactionNo { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("entryType")]
    public string? EntryType { get; set; }
}

/// <summary>
/// Convertidor personalizado para JsonElement
/// </summary>
public class JsonElementConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            return doc.RootElement.Clone();
        }
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is JsonElement element)
        {
            element.WriteTo(writer);
        }
        else if (value != null)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// Estados de conexión
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    PairingRequired,
    Paired,
    Busy,
    Error
}
