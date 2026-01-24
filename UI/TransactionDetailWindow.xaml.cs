using CloverBridge.Models;
using CloverBridge.Services;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace CloverBridge.UI;

public partial class TransactionDetailWindow : Window
{
    private readonly TransactionFile _transaction;
    private readonly string _jsonRaw;

    public TransactionDetailWindow(TransactionFile transaction, string jsonRaw)
    {
        InitializeComponent();
        _transaction = transaction;
        _jsonRaw = jsonRaw;
        LoadTransactionData();
    }

    private void LoadTransactionData()
    {
        // Header
        InvoiceNumberText.Text = $"Factura: {_transaction.InvoiceNumber}";

        // Información General
        TransactionIdText.Text = _transaction.TransactionId ?? "N/A";
        ExternalIdText.Text = _transaction.ExternalId ?? "N/A";
        StatusText.Text = _transaction.Status.ToString();
        TypeText.Text = _transaction.Type ?? "N/A";
        AmountText.Text = $"${_transaction.Amount:F2}";
        TaxText.Text = _transaction.Tax.HasValue ? $"${_transaction.Tax.Value:F2}" : "N/A";
        CustomerNameText.Text = _transaction.CustomerName ?? "N/A";
        TimestampText.Text = _transaction.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        NotesText.Text = _transaction.Notes ?? "Sin notas";

        // Color del estado
        StatusText.Foreground = _transaction.Status switch
        {
            TransactionStatus.Successful => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
            TransactionStatus.Cancelled => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),
            TransactionStatus.Failed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
            TransactionStatus.Timeout => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
            TransactionStatus.Pending => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            TransactionStatus.Processing => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184))
        };

        // Información de Pago
        if (_transaction.PaymentInfo != null)
        {
            var pi = _transaction.PaymentInfo;
            CloverPaymentIdText.Text = pi.CloverPaymentId ?? "N/A";
            CloverOrderIdText.Text = pi.CloverOrderId ?? "N/A";
            
            string cardInfo = "N/A";
            if (!string.IsNullOrEmpty(pi.CardBrand) && !string.IsNullOrEmpty(pi.CardLast4))
            {
                cardInfo = $"{pi.CardBrand} •••• {pi.CardLast4}";
            }
            CardInfoText.Text = cardInfo;
            
            AuthCodeText.Text = pi.AuthCode ?? "N/A";
            TipText.Text = pi.Tip.HasValue ? $"${pi.Tip.Value:F2}" : "N/A";
            TotalAmountText.Text = $"${pi.TotalAmount:F2}";
            ReceiptNumberText.Text = pi.ReceiptNumber ?? "N/A";
            ProcessingFeeText.Text = pi.ProcessingFee.HasValue ? $"${pi.ProcessingFee.Value:F2}" : "N/A";
            
            PaymentInfoBorder.Visibility = Visibility.Visible;
        }
        else
        {
            PaymentInfoBorder.Visibility = Visibility.Collapsed;
        }

        // Error (si existe)
        if (!string.IsNullOrEmpty(_transaction.ErrorMessage))
        {
            ErrorBorder.Visibility = Visibility.Visible;
            ErrorMessageText.Text = _transaction.ErrorMessage;
            ErrorCodeText.Text = !string.IsNullOrEmpty(_transaction.ErrorCode) 
                ? $"Código: {_transaction.ErrorCode}" 
                : "";
        }

        // Log de Transacción
        if (_transaction.TransactionLog != null && _transaction.TransactionLog.Any())
        {
            TransactionLogItems.ItemsSource = _transaction.TransactionLog.OrderBy(l => l.Timestamp);
        }

        // Tiempos de Procesamiento
        ProcessStartTimeText.Text = _transaction.ProcessStartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        SentToTerminalTimeText.Text = _transaction.SentToTerminalTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        ProcessEndTimeText.Text = _transaction.ProcessEndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        if (_transaction.ProcessStartTime.HasValue && _transaction.ProcessEndTime.HasValue)
        {
            var duration = _transaction.ProcessEndTime.Value - _transaction.ProcessStartTime.Value;
            DurationText.Text = $"{duration.TotalSeconds:F1} segundos";
        }
        else
        {
            DurationText.Text = "En proceso...";
        }

        // JSON Raw (formateado)
        try
        {
            var jsonDoc = JsonDocument.Parse(_jsonRaw);
            var formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            JsonRawText.Text = formatted;
        }
        catch
        {
            JsonRawText.Text = _jsonRaw;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(JsonRawText.Text);
            MessageBox.Show("JSON copiado al portapapeles", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al copiar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
