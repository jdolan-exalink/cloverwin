using System;
using System.Collections.Generic;
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
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "SALE"; // SALE, REFUND, VOID, etc

    [JsonPropertyName("detail")]
    public TransactionDetail Detail { get; set; } = new();

    [JsonPropertyName("paymentInfo")]
    public PaymentFileInfo? PaymentInfo { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Estados posibles de una transacción
/// </summary>
public enum TransactionStatus
{
    Pending,      // Pendiente (en INBOX)
    Processing,   // Procesándose en Clover
    Completed,    // Completada exitosamente
    Approved,     // Aprobada por usuario
    Rejected,     // Rechazada por usuario
    Cancelled,    // Cancelada por usuario o timeout
    Failed,       // Error durante procesamiento
    Reversed      // Reversada/reembolsada
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

    // Nuevos campos para cancelación y timeout
    [JsonPropertyName("cancelledReason")]
    public string? CancelledReason { get; set; }

    [JsonPropertyName("cancelledBy")]
    public string? CancelledBy { get; set; }

    [JsonPropertyName("cancelledTimestamp")]
    public DateTime? CancelledTimestamp { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("terminalTimeoutDefault")]
    public int TerminalTimeoutDefault { get; set; } = 30; // 30 segundos por defecto

    [JsonPropertyName("processingStartTime")]
    public DateTime? ProcessingStartTime { get; set; }}