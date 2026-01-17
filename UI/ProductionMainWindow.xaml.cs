using CloverBridge.Models;
using CloverBridge.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CloverBridge.UI;

public class TransactionRecord
{
    public string Timestamp { get; set; } = "";
    public string Type { get; set; } = "";
    public string Amount { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public partial class ProductionMainWindow : Window
{
    private readonly CloverWebSocketService _cloverService;
    private readonly ConfigurationService _configService;
    private readonly TransactionQueueService _queueService;
    private readonly InboxWatcherService _inboxService;
    private readonly ObservableCollection<TransactionRecord> _transactions;
    private readonly DispatcherTimer _uptimeTimer;
    private DateTime _startTime;
    
    // Contadores
    private int _totalPayments = 0;
    private decimal _totalPaymentsAmount = 0;
    private int _totalRefunds = 0;
    private decimal _totalRefundsAmount = 0;
    private int _totalErrors = 0;
    
    private bool _isExiting = false;
    public bool ForceClose { get; set; } = false;

    public ProductionMainWindow(
        CloverWebSocketService cloverService,
        ConfigurationService configService,
        TransactionQueueService queueService,
        InboxWatcherService inboxService)
    {
        InitializeComponent();

        _cloverService = cloverService;
        _configService = configService;
        _queueService = queueService;
        _inboxService = inboxService;
        _transactions = new ObservableCollection<TransactionRecord>();
        _startTime = DateTime.Now;

        // Mostrar versión
        VersionTextBlock.Text = AppVersion.GetVersion();

        // Configurar DataGrid
        TransactionsDataGrid.ItemsSource = _transactions;

        // Timer para uptime
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += UpdateUptime;
        _uptimeTimer.Start();

        // Cargar configuración
        LoadConfiguration();

        // Suscribirse a eventos
        _cloverService.StateChanged += OnCloverStateChanged;
        _cloverService.PairingCodeReceived += OnPairingCodeReceived;
        _cloverService.MessageReceived += OnCloverMessageReceived;

        // Actualizar estado inicial
        UpdateConnectionStatus(_cloverService.State);

        // Generar External ID inicial
        ExternalIdTextBox.Text = GenerateExternalId();

        // Cargar historial de transacciones
        LoadTransactionHistory();

        // Inicializar desglose de productos
        UpdateProductSummary();

        LogSystem("🚀 CloverBridge Professional iniciado");
        LogSystem($"📊 Sistema listo para operar");
    }

    private void UpdateUptime(object? sender, EventArgs e)
    {
        var uptime = DateTime.Now - _startTime;
        UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configService.GetConfig();
            HostTextBox.Text = config.Clover.Host ?? "10.1.1.53";
            PortTextBox.Text = config.Clover.Port.ToString();
            MerchantIdTextBox.Text = config.Clover.RemoteAppId ?? "clover-bridge";
            DeviceIdTextBox.Text = config.Clover.SerialNumber ?? "CB-001";
            TokenTextBox.Text = config.Clover.AuthToken ?? "";
            SecureCheckBox.IsChecked = config.Clover.Secure;
            
            LogSystem($"⚙️ Configuración cargada: {config.Clover.Host}:{config.Clover.Port}");
        }
        catch (Exception ex)
        {
            LogSystem($"⚠️ Error cargando configuración: {ex.Message}");
        }
    }

    private void OnCloverStateChanged(object? sender, ConnectionState state)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(state));
    }

    private void UpdateConnectionStatus(ConnectionState state)
    {
        var statusInfo = state switch
        {
            ConnectionState.Connected => ("✅ Conectado", "#10b981"),
            ConnectionState.Paired => ("🔐 Integrado", "#10b981"),
            ConnectionState.PairingRequired => ("⚠️ Requiere Integración", "#f59e0b"),
            ConnectionState.Connecting => ("🔄 Conectando...", "#667eea"),
            ConnectionState.Disconnected => ("⭕ Desconectado", "#ef4444"),
            ConnectionState.Error => ("❌ Error", "#ef4444"),
            _ => ("❓ Desconocido", "#64748b")
        };

        ConnectionStatusText.Text = statusInfo.Item1;
        ConnectionStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusInfo.Item2));

        // Actualizar botones
        var isPaired = state == ConnectionState.Paired;
        var isConnected = state == ConnectionState.Connected || isPaired;
        
        DisconnectButton.IsEnabled = isConnected;
        ConnectButton.IsEnabled = !isConnected;
        UnpairButton.IsEnabled = isPaired;

        if (isPaired)
        {
            LogSystem("✅ Terminal integrado correctamente");
        }
    }

    private void OnPairingCodeReceived(object? sender, string code)
    {
        Dispatcher.Invoke(() =>
        {
            PopupPairingCode.Text = code;
            PopupPairingStatus.Text = "✅ Ingresa este código en el terminal Clover";
            LogSystem($"🔑 Código de integración: {code}");
        });
    }

    private void OnCloverMessageReceived(object? sender, CloverMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var payload = message.Payload != null ? JsonSerializer.Serialize(message.Payload) : "(sin payload)";
                LogSystem($"📨 {message.Method} #{message.Id} -> {payload}");
            }
            catch (Exception ex)
            {
                LogSystem($"📨 {message.Method} #{message.Id}");
                LogSystem($"⚠️ Error serializando payload: {ex.Message}");
            }
        });
    }

    private void AddTransaction(string type, JsonElement data)
    {
        var record = new TransactionRecord
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Type = type,
            Amount = data.TryGetProperty("amount", out var amt) ? $"${amt.GetDecimal() / 100m:F2}" : "N/A",
            TransactionId = data.TryGetProperty("id", out var id) ? id.GetString() ?? "N/A" : "N/A",
            Status = data.TryGetProperty("success", out var suc) && suc.GetBoolean() ? "✅ Exitoso" : "❌ Fallido",
            Message = data.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : ""
        };

        _transactions.Insert(0, record);
        
        // Limitar a 100 registros
        while (_transactions.Count > 100)
        {
            _transactions.RemoveAt(_transactions.Count - 1);
        }

        SaveTransactionHistory();
    }

    private void UpdateMetrics()
    {
        TotalPaymentsText.Text = _totalPayments.ToString();
        TotalPaymentsAmountText.Text = $"${_totalPaymentsAmount:F2}";
        
        TotalRefundsText.Text = _totalRefunds.ToString();
        TotalRefundsAmountText.Text = $"${_totalRefundsAmount:F2}";
        
        TotalErrorsText.Text = _totalErrors.ToString();
    }

    private void LoadTransactionHistory()
    {
        try
        {
            var historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transaction-history.json");
            if (File.Exists(historyPath))
            {
                var json = File.ReadAllText(historyPath);
                var history = JsonSerializer.Deserialize<TransactionHistory>(json);
                
                if (history != null)
                {
                    _totalPayments = history.TotalPayments;
                    _totalPaymentsAmount = history.TotalPaymentsAmount;
                    _totalRefunds = history.TotalRefunds;
                    _totalRefundsAmount = history.TotalRefundsAmount;
                    _totalErrors = history.TotalErrors;
                    
                    foreach (var record in history.Transactions.Take(100))
                    {
                        _transactions.Add(record);
                    }
                    
                    UpdateMetrics();
                    LogSystem($"📊 Historial cargado: {_totalPayments} pagos, {_totalRefunds} devoluciones");
                }
            }
        }
        catch (Exception ex)
        {
            LogSystem($"⚠️ Error cargando historial: {ex.Message}");
        }
    }

    private void SaveTransactionHistory()
    {
        try
        {
            var history = new TransactionHistory
            {
                TotalPayments = _totalPayments,
                TotalPaymentsAmount = _totalPaymentsAmount,
                TotalRefunds = _totalRefunds,
                TotalRefundsAmount = _totalRefundsAmount,
                TotalErrors = _totalErrors,
                Transactions = _transactions.ToList()
            };

            var historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transaction-history.json");
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(historyPath, json);
        }
        catch (Exception ex)
        {
            LogSystem($"⚠️ Error guardando historial: {ex.Message}");
        }
    }

    private void LogSystem(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}\n";
        
        SystemLogTextBox.AppendText(logEntry);
        SystemLogTextBox.ScrollToEnd();
        
        FooterStatusText.Text = message;
    }

    private string GenerateExternalId()
    {
        return $"TXN-{DateTime.Now:yyyyMMddHHmmss}";
    }

    // Event Handlers

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogSystem("🔄 Conectando al terminal...");
            await _cloverService.ConnectAsync();
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error conectando: {ex.Message}");
            MessageBox.Show($"Error al conectar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogSystem("⚠️ Desconectando del terminal...");
            await _cloverService.DisconnectAsync();
            LogSystem("✅ Desconectado correctamente");
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error desconectando: {ex.Message}");
        }
    }

    private void PairingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cloverService.State != ConnectionState.Connected && _cloverService.State != ConnectionState.PairingRequired)
        {
            MessageBox.Show("Debe estar conectado al terminal para iniciar la integración", "Advertencia", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PairingPopup.Visibility = Visibility.Visible;
        PopupPairingCode.Text = "------";
        PopupPairingStatus.Text = "🔄 Iniciando integración...";
        
        LogSystem("🔑 Iniciando proceso de integración...");
        
        Task.Run(async () =>
        {
            try
            {
                await _cloverService.SendPairingRequestAsync();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogSystem($"❌ Error en integración: {ex.Message}");
                    PopupPairingStatus.Text = $"❌ Error: {ex.Message}";
                });
            }
        });
    }

    private async void UnpairButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Está seguro que desea desintegrar el terminal?\n\nDeberá volver a integrarlo para procesar transacciones.",
            "Confirmar Desintegración",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                LogSystem("🔓 Desintegrando terminal...");
                
                var config = _configService.GetConfig();
                config.Clover.AuthToken = null;
                _configService.UpdateConfig(config);
                
                await _cloverService.DisconnectAsync();
                await Task.Delay(1000);
                await _cloverService.ConnectAsync();
                
                LogSystem("✅ Terminal desintegrado. Puede volver a integrarlo cuando lo necesite.");
                MessageBox.Show("Terminal desintegrado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogSystem($"❌ Error desintegrando: {ex.Message}");
                MessageBox.Show($"Error al desintegrar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void SendSaleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_cloverService.State != ConnectionState.Paired)
            {
                MessageBox.Show("Debe estar integrado con el terminal para enviar pagos", "Advertencia",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validar datos
            var invoiceNumber = InvoiceNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                MessageBox.Show("Por favor ingrese el número de factura", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                InvoiceNumberTextBox.Focus();
                return;
            }

            // Función auxiliar para parsear decimales con soporte a coma y punto
            bool TryParsePrice(string text, out decimal value)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0;
                    return false;
                }
                // Normalizar: reemplazar coma por punto para conversión
                var normalized = text.Replace(",", ".");
                return decimal.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            // Validar Producto 1
            if (!TryParsePrice(Product1PriceTextBox.Text, out var product1Price) || product1Price <= 0)
            {
                MessageBox.Show("Precio del Producto 1 inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Product1PriceTextBox.Focus();
                return;
            }

            if (!int.TryParse(Product1QtyTextBox.Text, out var product1Qty) || product1Qty <= 0)
            {
                MessageBox.Show("Cantidad del Producto 1 inválida", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Product1QtyTextBox.Focus();
                return;
            }

            // Validar Producto 2
            if (!TryParsePrice(Product2PriceTextBox.Text, out var product2Price) || product2Price <= 0)
            {
                MessageBox.Show("Precio del Producto 2 inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Product2PriceTextBox.Focus();
                return;
            }

            if (!int.TryParse(Product2QtyTextBox.Text, out var product2Qty) || product2Qty <= 0)
            {
                MessageBox.Show("Cantidad del Producto 2 inválida", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Product2QtyTextBox.Focus();
                return;
            }

            // Calcular totales
            var product1Total = product1Price * product1Qty;
            var product2Total = product2Price * product2Qty;
            var totalAmount = product1Total + product2Total;

            var externalId = string.IsNullOrWhiteSpace(ExternalIdTextBox.Text) ? GenerateExternalId() : ExternalIdTextBox.Text;

            // Crear items de línea
            var items = new List<LineItem>
            {
                new LineItem
                {
                    ProductId = "PROD-001",
                    ProductName = Product1NameTextBox.Text.Trim(),
                    Quantity = product1Qty,
                    UnitPrice = product1Price
                },
                new LineItem
                {
                    ProductId = "PROD-002",
                    ProductName = Product2NameTextBox.Text.Trim(),
                    Quantity = product2Qty,
                    UnitPrice = product2Price
                }
            };

            // Crear archivo de transacción
            var fileService = new TransactionFileService(_configService);
            var transactionFile = fileService.CreateTransactionFile(invoiceNumber, externalId, totalAmount, items);

            // Inicializar info de pago con timestamp y timeout por defecto
            transactionFile.PaymentInfo = new PaymentFileInfo
            {
                TotalAmount = totalAmount,
                ProcessingStartTime = DateTime.Now,
                TerminalTimeoutDefault = 30 // 30 segundos por defecto
            };

            LogSystem($"📄 Transacción creada: {invoiceNumber}");
            LogSystem($"  📦 Producto 1: {items[0].ProductName} x{items[0].Quantity} = ${product1Total:F2}");
            LogSystem($"  📦 Producto 2: {items[1].ProductName} x{items[1].Quantity} = ${product2Total:F2}");
            LogSystem($"  💰 Total: ${totalAmount:F2}");
            LogSystem($"  ⏱️  Timeout por defecto: {transactionFile.PaymentInfo.TerminalTimeoutDefault}s");

            // Guardar a OUTBOX (archivo de entrada/salida)
            await fileService.WriteTransactionToOutboxAsync(transactionFile);
            LogSystem($"💾 Archivo guardado en OUTBOX para seguimiento");

            // Enviar pago a Clover con timeout
            LogSystem($"💳 Enviando pago de ${totalAmount:F2} (Factura: {invoiceNumber})...");
            FooterStatusText.Text = "Procesando pago en terminal...";

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(transactionFile.PaymentInfo.TerminalTimeoutDefault));
            var responseTask = _cloverService.SendSaleAsync(totalAmount, externalId, 0);
            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            CloverMessage? response = null;
            bool timedOut = false;

            if (completedTask == timeoutTask)
            {
                // Timeout alcanzado
                timedOut = true;
                LogSystem($"⏱️  TIMEOUT: Transacción no respondió en {transactionFile.PaymentInfo.TerminalTimeoutDefault}s");
                transactionFile.Status = TransactionStatus.Cancelled;
                transactionFile.Result = "TIMEOUT";
                transactionFile.Message = $"Timeout después de {transactionFile.PaymentInfo.TerminalTimeoutDefault} segundos";
                
                if (transactionFile.PaymentInfo != null)
                {
                    transactionFile.PaymentInfo.TimeoutSeconds = transactionFile.PaymentInfo.TerminalTimeoutDefault;
                    transactionFile.PaymentInfo.CancelledReason = "Timeout en terminal";
                    transactionFile.PaymentInfo.CancelledTimestamp = DateTime.Now;
                }
            }
            else
            {
                // Respuesta recibida
                response = await responseTask;
                var success = response?.Method?.Contains("RESPONSE") == true;

                if (success)
                {
                    transactionFile.Status = TransactionStatus.Completed;
                    transactionFile.Result = "COMPLETED";
                    transactionFile.Message = "Transacción completada exitosamente";
                    LogSystem($"✅ Pago aprobado");
                }
                else
                {
                    // Verificar si fue cancelado en el terminal
                    transactionFile.Status = TransactionStatus.Cancelled;
                    transactionFile.Result = "DECLINED";
                    transactionFile.Message = "Pago rechazado o cancelado en terminal";
                    
                    if (transactionFile.PaymentInfo != null)
                    {
                        transactionFile.PaymentInfo.CancelledReason = "Cancelado/Rechazado en terminal";
                        transactionFile.PaymentInfo.CancelledTimestamp = DateTime.Now;
                        transactionFile.PaymentInfo.CancelledBy = "Usuario en terminal";
                    }
                    
                    LogSystem($"❌ Pago rechazado o cancelado");
                }
            }

            // Guardar resultado actualizado a OUTBOX
            await fileService.WriteTransactionToOutboxAsync(transactionFile);
            LogSystem($"💾 Resultado guardado: {transactionFile.Status}");

            if (response != null)
            {
                var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                LogSystem($"📥 Respuesta:\n{responseJson}");
            }

            // Registrar en transacciones - crear JsonElement con datos de la transacción
            var transactionData = new
            {
                amount = (long)(totalAmount * 100), // Convertir a centavos
                id = externalId,
                success = transactionFile.Status == TransactionStatus.Completed,
                message = transactionFile.Message,
                status = transactionFile.Status.ToString()
            };
            var dataJson = JsonSerializer.SerializeToElement(transactionData);
            AddTransaction("SALE", dataJson);

            // Reiniciar formulario
            ExternalIdTextBox.Text = GenerateExternalId();
            InvoiceNumberTextBox.Clear();

            // Actualizar estado en footer
            FooterStatusText.Text = transactionFile.Status switch
            {
                TransactionStatus.Completed => "✅ Pago procesado exitosamente",
                TransactionStatus.Cancelled => "⏱️ Pago cancelado/timeout",
                TransactionStatus.Failed => "❌ Pago rechazado",
                _ => "ℹ️ Pago pendiente de aprobación"
            };
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error enviando pago: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Error al enviar pago: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error procesando pago";
        }
    }

    private void RecalculateTotal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Función auxiliar para parsear decimales con soporte a coma y punto
            bool TryParsePrice(string text, out decimal value)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0;
                    return false;
                }
                // Normalizar: reemplazar coma por punto para conversión
                var normalized = text.Replace(",", ".");
                return decimal.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            if (TryParsePrice(Product1PriceTextBox.Text, out var price1) &&
                int.TryParse(Product1QtyTextBox.Text, out var qty1) &&
                TryParsePrice(Product2PriceTextBox.Text, out var price2) &&
                int.TryParse(Product2QtyTextBox.Text, out var qty2))
            {
                var product1Name = Product1NameTextBox.Text.Trim();
                var product2Name = Product2NameTextBox.Text.Trim();
                
                var total1 = price1 * qty1;
                var total2 = price2 * qty2;
                var total = total1 + total2;
                
                // Actualizar desglose
                Product1SummaryTextBlock.Text = $"{product1Name}: {qty1} × ${price1:F2} = ${total1:F2}";
                Product2SummaryTextBlock.Text = $"{product2Name}: {qty2} × ${price2:F2} = ${total2:F2}";
                TotalAmountTextBlock.Text = $"Total: ${total:F2}";
            }
        }
        catch
        {
            TotalAmountTextBlock.Text = "Total: Error en cálculo";
        }
    }

    // Métodos de testing removidos - solo Nueva Venta disponible
    /*
    private async void GenerateQRButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!decimal.TryParse(QRAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var amountInCents = (int)(amount * 100);

            LogSystem($"📱 Generando QR Code de ${amount:F2}...");

            var request = new
            {
                type = "QR",
                amount = amountInCents,
                timestamp = DateTime.Now.ToString("o")
            };

            var json = JsonSerializer.Serialize(request);
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "INBOX",
                $"qr_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(filePath, json);

            LogSystem($"✅ QR Code generado: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error generando QR: {ex.Message}");
            MessageBox.Show($"Error al generar QR: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SendAuthButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!decimal.TryParse(AuthAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var amountInCents = (int)(amount * 100);

            LogSystem($"🔒 Enviando autorización de ${amount:F2}...");

            var request = new
            {
                type = "AUTH",
                amount = amountInCents,
                externalId = GenerateExternalId(),
                timestamp = DateTime.Now.ToString("o")
            };

            var json = JsonSerializer.Serialize(request);
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "INBOX",
                $"auth_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(filePath, json);

            LogSystem($"✅ Autorización enviada: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error enviando autorización: {ex.Message}");
            MessageBox.Show($"Error al enviar autorización: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PaymentIdTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese un Payment ID", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"🚫 Anulando pago {PaymentIdTextBox.Text}...");

            var request = new
            {
                type = "VOID",
                paymentId = PaymentIdTextBox.Text,
                timestamp = DateTime.Now.ToString("o")
            };

            var json = JsonSerializer.Serialize(request);
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "INBOX",
                $"void_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(filePath, json);

            LogSystem($"✅ Anulación enviada: {Path.GetFileName(filePath)}");
            PaymentIdTextBox.Clear();
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error anulando: {ex.Message}");
            MessageBox.Show($"Error al anular: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefundButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PaymentIdTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese un Payment ID", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"💰 Devolviendo pago {PaymentIdTextBox.Text}...");

            var request = new
            {
                type = "REFUND",
                paymentId = PaymentIdTextBox.Text,
                timestamp = DateTime.Now.ToString("o")
            };

            var json = JsonSerializer.Serialize(request);
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "INBOX",
                $"refund_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(filePath, json);

            LogSystem($"✅ Devolución enviada: {Path.GetFileName(filePath)}");
            PaymentIdTextBox.Clear();
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error devolviendo: {ex.Message}");
            MessageBox.Show($"Error al devolver: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    */

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = _configService.GetConfig();
            
            config.Clover.Host = HostTextBox.Text;
            config.Clover.Port = int.TryParse(PortTextBox.Text, out var port) ? port : 12345;
            config.Clover.RemoteAppId = MerchantIdTextBox.Text;
            config.Clover.SerialNumber = DeviceIdTextBox.Text;
            config.Clover.AuthToken = string.IsNullOrWhiteSpace(TokenTextBox.Text) ? null : TokenTextBox.Text;
            config.Clover.Secure = SecureCheckBox.IsChecked ?? false;

            _configService.UpdateConfig(config);

            LogSystem("✅ Configuración guardada correctamente");
            MessageBox.Show("Configuración guardada. Reiniciando conexión...", "Éxito", 
                MessageBoxButton.OK, MessageBoxImage.Information);

            Task.Run(async () =>
            {
                await _cloverService.DisconnectAsync();
                await Task.Delay(1000);
                await _cloverService.ConnectAsync();
            });
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error guardando configuración: {ex.Message}");
            MessageBox.Show($"Error al guardar configuración: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenConfigFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = AppDomain.CurrentDomain.BaseDirectory;
            Process.Start("explorer.exe", configPath);
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error abriendo carpeta: {ex.Message}");
        }
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }
            Process.Start("explorer.exe", logsPath);
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error abriendo logs: {ex.Message}");
        }
    }

    private void TransactionFilter_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // TODO: Implementar filtrado de transacciones
    }

    private void RefreshTransactions_Click(object sender, RoutedEventArgs e)
    {
        LogSystem("🔄 Actualizando transacciones...");
        // Ya están en memoria, solo refrescar vista
        TransactionsDataGrid.Items.Refresh();
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Está seguro que desea limpiar el historial de transacciones?",
            "Confirmar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            _transactions.Clear();
            _totalPayments = 0;
            _totalPaymentsAmount = 0;
            _totalRefunds = 0;
            _totalRefundsAmount = 0;
            _totalErrors = 0;
            UpdateMetrics();
            SaveTransactionHistory();
            LogSystem("🗑️ Historial limpiado");
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        SystemLogTextBox.Clear();
        LogSystem("🗑️ Logs limpiados");
    }

    /*
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
        this.Hide();
        LogSystem("➖ Ventana minimizada al systray");
    }
    */

    private void ClosePairingPopup_Click(object sender, RoutedEventArgs e)
    {
        PairingPopup.Visibility = Visibility.Collapsed;
    }

    private void RetryPairingButton_Click(object sender, RoutedEventArgs e)
    {
        PopupPairingCode.Text = "------";
        PopupPairingStatus.Text = "🔄 Reintentando integración...";
        
        Task.Run(async () =>
        {
            try
            {
                await _cloverService.SendPairingRequestAsync();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogSystem($"❌ Error reintentando: {ex.Message}");
                    PopupPairingStatus.Text = $"❌ Error: {ex.Message}";
                });
            }
        });
    }

    private void PairingPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Cerrar popup al hacer clic fuera
        PairingPopup.Visibility = Visibility.Collapsed;
    }

    private void PairingPopupContent_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Evitar que se cierre al hacer clic en el contenido
        e.Handled = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ForceClose && !_isExiting)
        {
            e.Cancel = true;
            this.Hide();
            LogSystem("➖ Aplicación minimizada al systray");
        }
        else
        {
            _uptimeTimer.Stop();
            SaveTransactionHistory();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
        }
    }

    public void ShowWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        LogSystem("⬆️ Ventana restaurada");
    }

    private void UpdateProductSummary()
    {
        try
        {
            // Función auxiliar para parsear decimales con soporte a coma y punto
            bool TryParsePrice(string text, out decimal value)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0;
                    return false;
                }
                // Normalizar: reemplazar coma por punto para conversión
                var normalized = text.Replace(",", ".");
                return decimal.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            if (TryParsePrice(Product1PriceTextBox.Text, out var price1) &&
                int.TryParse(Product1QtyTextBox.Text, out var qty1) &&
                TryParsePrice(Product2PriceTextBox.Text, out var price2) &&
                int.TryParse(Product2QtyTextBox.Text, out var qty2))
            {
                var product1Name = Product1NameTextBox.Text.Trim();
                var product2Name = Product2NameTextBox.Text.Trim();
                
                var total1 = price1 * qty1;
                var total2 = price2 * qty2;
                var total = total1 + total2;
                
                // Actualizar desglose
                Product1SummaryTextBlock.Text = $"{product1Name}: {qty1} × ${price1:F2} = ${total1:F2}";
                Product2SummaryTextBlock.Text = $"{product2Name}: {qty2} × ${price2:F2} = ${total2:F2}";
                TotalAmountTextBlock.Text = $"Total: ${total:F2}";
            }
        }
        catch
        {
            // Ignorar errores en inicialización
        }
    }

    // ============ OUTBOX Management Methods ============

    private void RefreshOutboxButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileService = new TransactionFileService(_configService);
            var config = _configService.GetConfig();
            var outboxPath = config.Folders.Outbox;

            OutboxFileListBox.Items.Clear();

            if (Directory.Exists(outboxPath))
            {
                var files = Directory.GetFiles(outboxPath, "*.json")
                    .Select(Path.GetFileName)
                    .OrderByDescending(f => f)
                    .ToList();

                foreach (var file in files)
                {
                    OutboxFileListBox.Items.Add(file);
                }

                LogSystem($"📂 OUTBOX: {files.Count} archivos encontrados");
            }
            else
            {
                LogSystem("⚠️ OUTBOX folder not found");
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error refreshing OUTBOX: {ex.Message}");
        }
    }

    private void OutboxFileListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (OutboxFileListBox.SelectedItem is string filename)
        {
            LoadOutboxFileDetails(filename);
        }
    }

    private void LoadOutboxFileDetails(string filename)
    {
        try
        {
            var config = _configService.GetConfig();
            var filepath = Path.Combine(config.Folders.Outbox, filename);

            if (File.Exists(filepath))
            {
                var json = File.ReadAllText(filepath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var parsed = JsonDocument.Parse(json);
                var formatted = JsonSerializer.Serialize(parsed, options);

                OutboxDetailsTextBox.Text = formatted;
                LogSystem($"📄 Archivo cargado: {filename}");
            }
        }
        catch (Exception ex)
        {
            OutboxDetailsTextBox.Text = $"Error loading file: {ex.Message}";
            LogSystem($"❌ Error loading file: {ex.Message}");
        }
    }

    private void ViewOutboxDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (OutboxFileListBox.SelectedItem is string filename)
        {
            LoadOutboxFileDetails(filename);
        }
        else
        {
            MessageBox.Show("Por favor seleccione un archivo de OUTBOX", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ApproveTransactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (OutboxFileListBox.SelectedItem is not string filename)
        {
            MessageBox.Show("Por favor seleccione un archivo de OUTBOX", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fileService = new TransactionFileService(_configService);
            var config = _configService.GetConfig();
            var filepath = Path.Combine(config.Folders.Outbox, filename);

            var json = File.ReadAllText(filepath);
            var transaction = JsonSerializer.Deserialize<TransactionFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (transaction != null)
            {
                // Registrar aprobación
                transaction.Status = TransactionStatus.Approved;
                transaction.Message = "Aprobado por usuario en control panel";
                
                if (transaction.PaymentInfo != null && transaction.Status == TransactionStatus.Approved)
                {
                    transaction.PaymentInfo.CancelledBy = "Aprobado por usuario";
                    transaction.PaymentInfo.CancelledTimestamp = DateTime.Now;
                }

                // Archivar con estado actualizado
                await fileService.ArchiveTransactionAsync(transaction, filename);

                // Eliminar de OUTBOX
                File.Delete(filepath);

                var invoiceNum = transaction.Detail?.InvoiceNumber ?? "Unknown";
                LogSystem($"✅ Transacción aprobada: {invoiceNum}");
                LogSystem($"   Monto: ${transaction.PaymentInfo?.TotalAmount:F2}");
                LogSystem($"   Archivado para historial");
                
                RefreshOutboxButton_Click(null, null);
                OutboxDetailsTextBox.Clear();
                MessageBox.Show($"Transacción {invoiceNum} aprobada y archivada", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error aprobando transacción: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RejectTransactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (OutboxFileListBox.SelectedItem is not string filename)
        {
            MessageBox.Show("Por favor seleccione un archivo de OUTBOX", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fileService = new TransactionFileService(_configService);
            var config = _configService.GetConfig();
            var filepath = Path.Combine(config.Folders.Outbox, filename);

            var json = File.ReadAllText(filepath);
            var transaction = JsonSerializer.Deserialize<TransactionFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (transaction != null)
            {
                // Registrar rechazo con detalles
                transaction.Status = TransactionStatus.Rejected;
                transaction.Message = "Rechazado por usuario en control panel";
                
                if (transaction.PaymentInfo != null)
                {
                    transaction.PaymentInfo.CancelledReason = "Rechazado por usuario";
                    transaction.PaymentInfo.CancelledBy = "Usuario en control panel";
                    transaction.PaymentInfo.CancelledTimestamp = DateTime.Now;
                }

                // Archivar con estado actualizado
                await fileService.ArchiveTransactionAsync(transaction, filename);

                // Eliminar de OUTBOX
                File.Delete(filepath);

                var invoiceNum = transaction.Detail?.InvoiceNumber ?? "Unknown";
                LogSystem($"❌ Transacción rechazada: {invoiceNum}");
                LogSystem($"   Monto: ${transaction.PaymentInfo?.TotalAmount:F2}");
                LogSystem($"   Razón: Rechazado en control panel");
                LogSystem($"   Archivado para historial");
                
                RefreshOutboxButton_Click(null, null);
                OutboxDetailsTextBox.Clear();
                MessageBox.Show($"Transacción {invoiceNum} rechazada y archivada", "Operación completada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error rechazando transacción: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ArchiveTransactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (OutboxFileListBox.SelectedItem is not string filename)
        {
            MessageBox.Show("Por favor seleccione un archivo de OUTBOX", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fileService = new TransactionFileService(_configService);
            var config = _configService.GetConfig();
            var filepath = Path.Combine(config.Folders.Outbox, filename);

            var json = File.ReadAllText(filepath);
            var transaction = JsonSerializer.Deserialize<TransactionFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (transaction != null)
            {
                // Archivar
                await fileService.ArchiveTransactionAsync(transaction, filename);

                // Eliminar de OUTBOX
                File.Delete(filepath);

                LogSystem($"📁 Transacción archivada: {transaction.Detail?.InvoiceNumber}");
                RefreshOutboxButton_Click(null, null);
                OutboxDetailsTextBox.Clear();
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error archivando transacción: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CleanupInboxButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileService = new TransactionFileService(_configService);
            var result = await fileService.CleanupInboxAsync();

            if (result)
            {
                LogSystem("🗑️ INBOX limpiado exitosamente");
                MessageBox.Show("INBOX limpiado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error limpiando INBOX: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
// Clase para historial de transacciones
public class TransactionHistory
{
    public int TotalPayments { get; set; }
    public decimal TotalPaymentsAmount { get; set; }
    public int TotalRefunds { get; set; }
    public decimal TotalRefundsAmount { get; set; }
    public int TotalErrors { get; set; }
    public List<TransactionRecord> Transactions { get; set; } = new();
}
