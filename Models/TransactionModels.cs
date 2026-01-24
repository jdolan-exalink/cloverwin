using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace CloverBridge.Models;

/// <summary>
/// Información de un producto
/// </summary>
public class Product
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("unitOfMeasure")]
    public string UnitOfMeasure { get; set; } = "unit";

    public decimal GetTotal() => Price * Quantity;
}

/// <summary>
/// Item de línea en transacción
/// </summary>
public class LineItem
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("discount")]
    public decimal? Discount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    public decimal GetSubtotal() => UnitPrice * Quantity;
    public decimal GetTotal() => GetSubtotal() - (Discount ?? 0);
}

/// <summary>
/// Transacción completa con detalles
/// </summary>
public class TransactionDetail
{
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("poNumber")]
    public string? PoNumber { get; set; }

    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("items")]
    public List<LineItem> Items { get; set; } = new();

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; set; }

    [JsonPropertyName("discount")]
    public decimal Discount { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    public decimal CalculateTotal()
    {
        var itemsTotal = Items.Sum(i => i.GetTotal());
        return itemsTotal + Tax - Discount;
    }
}

/// <summary>
/// Archivo de transacción para INBOX/OUTBOX
/// </summary>
public class TransactionFile
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "CLOVER";

    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "SALE"; // SALE, REFUND, VOID

    // Datos de la factura
    [JsonPropertyName("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; } // Monto total en pesos/dólares

    [JsonPropertyName("tax")]
    public decimal? Tax { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; } = "ARS";

    [JsonPropertyName("raw")]
    public object? Raw { get; set; }

    // Resultado del pago
    [JsonPropertyName("details")]
    public PaymentFileInfo? PaymentInfo { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    // Tracking de tiempos
    [JsonPropertyName("processStartTime")]
    public DateTime? ProcessStartTime { get; set; }

    [JsonPropertyName("processEndTime")]
    public DateTime? ProcessEndTime { get; set; }

    [JsonPropertyName("sentToTerminalTime")]
    public DateTime? SentToTerminalTime { get; set; }

    [JsonPropertyName("inboxFilePath")]
    public string? InboxFilePath { get; set; }

    // Tiempo restante para timeout (en segundos)
    [JsonPropertyName("timeoutRemainingSeconds")]
    public int? TimeoutRemainingSeconds { get; set; }

    // Lista de eventos de la transacción
    [JsonPropertyName("transactionLog")]
    public List<TransactionLogEntry> TransactionLog { get; set; } = new();

    public void AddLogEntry(string eventType, string description, string? details = null)
    {
        TransactionLog.Add(new TransactionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Description = description,
            Details = details
        });
    }
}

/// <summary>
/// Entrada de log para transacción
/// </summary>
public class TransactionLogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

/// <summary>
/// Estados posibles de una transacción
/// </summary>
[JsonConverter(typeof(TransactionStatusConverter))]
public enum TransactionStatus
{
    Pending,             // Pendiente (al enviar)
    Processing,          // Procesándose en Clover
    Successful,          // Exitosa (pagada correctamente)
    Cancelled,           // Cancelada por usuario
    Timeout,             // Timeout (120 segundos sin respuesta)
    InsufficientFunds,   // Sin fondos / tarjeta rechazada
    Failed               // Error durante procesamiento
}

/// <summary>
/// Información de pago para archivo
/// </summary>
public class PaymentFileInfo
{
    [JsonPropertyName("cloverPaymentId")]
    public string? CloverPaymentId { get; set; }

    [JsonPropertyName("cloverOrderId")]
    public string? CloverOrderId { get; set; }

    [JsonPropertyName("cardLast4")]
    public string? CardLast4 { get; set; }

    [JsonPropertyName("cardBrand")]
    public string? CardBrand { get; set; }

    [JsonPropertyName("authCode")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("receiptNumber")]
    public string? ReceiptNumber { get; set; }

    [JsonPropertyName("tip")]
    public decimal? Tip { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("processingFee")]
    public decimal? ProcessingFee { get; set; }

    // Nuevos campos para información detallada del pago
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; } // Ej: "MERCADO PAGO Transferencia", "VISA", etc.

    [JsonPropertyName("entryType")]
    public string? EntryType { get; set; } // Ej: "QR_CODE", "EMV_CONTACT", "SWIPE"

    [JsonPropertyName("currency")]
    public string? Currency { get; set; } // Ej: "ARS", "USD"

    [JsonPropertyName("paymentNote")]
    public string? PaymentNote { get; set; } // Nota con detalles adicionales

    [JsonPropertyName("tenderLabel")]
    public string? TenderLabel { get; set; } // Ej: "ar.com.fiserv.fiservqr.prod"

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("employeeId")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("merchantId")]
    public string? MerchantId { get; set; }

    [JsonPropertyName("transactionTime")]
    public DateTime? TransactionTime { get; set; } // Hora real de la transacción en Clover

    // Campos para cancelación y timeout
    [JsonPropertyName("cancelledReason")]
    public string? CancelledReason { get; set; }

    [JsonPropertyName("cancelledBy")]
    public string? CancelledBy { get; set; }

    [JsonPropertyName("cancelledTimestamp")]
    public DateTime? CancelledTimestamp { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("terminalTimeoutDefault")]
    public int TerminalTimeoutDefault { get; set; } = 30;

    [JsonPropertyName("processingStartTime")]
    public DateTime? ProcessingStartTime { get; set; }

    [JsonPropertyName("mp")]
    public MpPaymentDetail? Mp { get; set; }
}

public class MpPaymentDetail
{
    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; set; }

    [JsonPropertyName("date_approved")]
    public string? DateApproved { get; set; }

    [JsonPropertyName("pos_external_id")]
    public string? PosExternalId { get; set; }

    [JsonPropertyName("store_external_id")]
    public string? StoreExternalId { get; set; }
}

public class TransactionStatusConverter : JsonConverter<TransactionStatus>
{
    public override TransactionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (s == "APPROVED") return TransactionStatus.Successful;
        if (s == "PENDING") return TransactionStatus.Pending;
        if (s == "PROCESSING") return TransactionStatus.Processing;
        if (s == "REJECTED") return TransactionStatus.Cancelled;
        return TransactionStatus.Failed;
    }

    public override void Write(Utf8JsonWriter writer, TransactionStatus value, JsonSerializerOptions options)
    {
        var s = value switch
        {
            TransactionStatus.Successful => "APPROVED",
            TransactionStatus.Pending => "PENDING",
            TransactionStatus.Processing => "PROCESSING",
            TransactionStatus.Cancelled => "REJECTED",
            TransactionStatus.InsufficientFunds => "REJECTED",
            TransactionStatus.Timeout => "ERROR",
            TransactionStatus.Failed => "ERROR",
            _ => "UNKNOWN"
        };
        writer.WriteStringValue(s);
    }
}