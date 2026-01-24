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
    // private readonly DispatcherTimer _outboxMonitorTimer; // ELIMINADO - Ya no se usa
    private readonly DispatcherTimer _timeoutCounterTimer;
    private DateTime _startTime;
    private int _currentTimeoutSeconds = 0;
    private bool _isProcessingTransaction = false;
    
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

        // Timer para contador de timeout visible (cada 1 segundo)
        _timeoutCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timeoutCounterTimer.Tick += UpdateTimeoutCounter;
        _timeoutCounterTimer.Start();

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
        
        // Generar Invoice Number inicial
        GenerateNewInvoiceNumber();

        // Cargar historial de transacciones
        LoadTransactionHistory();

        LogSystem("🚀 CloverBridge Professional iniciado");
        LogSystem($"📊 Sistema listo para operar");
    }

    private void UpdateUptime(object? sender, EventArgs e)
    {
        var uptime = DateTime.Now - _startTime;
        UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private void UpdateTimeoutCounter(object? sender, EventArgs e)
    {
        if (_isProcessingTransaction && _currentTimeoutSeconds > 0)
        {
            TimeoutCounterText.Text = $"{_currentTimeoutSeconds}s";
            
            // Cambiar color según tiempo restante
            if (_currentTimeoutSeconds <= 15)
            {
                TimeoutCounterBorder.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Rojo
            }
            else if (_currentTimeoutSeconds <= 30)
            {
                TimeoutCounterBorder.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Naranja
            }
            else
            {
                TimeoutCounterBorder.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Azul
            }
        }
        else if (_isProcessingTransaction)
        {
            // Timeout alcanzado
            TimeoutCounterText.Text = "0s";
            TimeoutCounterBorder.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void ShowTimeoutCounter(int seconds)
    {
        _isProcessingTransaction = true;
        _currentTimeoutSeconds = seconds;
        TimeoutCounterBorder.Visibility = Visibility.Visible;
        TimeoutCounterText.Text = $"{seconds}s";
        TimeoutCounterBorder.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Azul inicial
    }

    private void HideTimeoutCounter()
    {
        _isProcessingTransaction = false;
        _currentTimeoutSeconds = 0;
        TimeoutCounterBorder.Visibility = Visibility.Collapsed;
    }

    private async void TransactionsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TransactionsDataGrid.SelectedItem is TransactionRecord record)
        {
            try
            {
                // Buscar la transacción en OUTBOX por InvoiceNumber del TransactionId
                var transactionId = record.TransactionId;
                
                // Leer desde OUTBOX
                var fileService = new TransactionFileService(_configService);
                var outboxService = new TransactionOutboxService(_configService);
                
                // Intentar obtener la transacción desde OUTBOX
                var transactions = await outboxService.ReadAllTransactionsFromOutboxAsync();
                var transaction = transactions.FirstOrDefault(t => 
                    t.TransactionId == transactionId || 
                    t.InvoiceNumber == transactionId);
                
                if (transaction != null)
                {
                    // Serializar para mostrar JSON
                    var jsonRaw = JsonSerializer.Serialize(transaction, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    // Mostrar ventana de detalles
                    var detailWindow = new TransactionDetailWindow(transaction, jsonRaw);
                    detailWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show(
                        "No se encontró el detalle completo de esta transacción en OUTBOX.",
                        "Transacción no encontrada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing transaction details");
                MessageBox.Show(
                    $"Error al mostrar detalles: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private async void MonitorOutboxTransactions_Removed(object? sender, EventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa el monitoreo de OUTBOX
        // Las transacciones se ven haciendo doble click en la tabla
    }

    private string GetStatusMessage(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Successful => "Transacción completada exitosamente",
            TransactionStatus.Processing => "Procesando en terminal...",
            TransactionStatus.Cancelled => "Transacción cancelada por el usuario",
            TransactionStatus.Timeout => "Timeout - Sin respuesta del terminal",
            TransactionStatus.InsufficientFunds => "Fondos insuficientes o tarjeta rechazada",
            TransactionStatus.Failed => "Error durante el procesamiento",
            _ => "Estado desconocido"
        };
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

    private void SaveConfiguration()
    {
        try
        {
            var config = _configService.GetConfig();
            
            // Guardar todos los valores de la UI
            config.Clover.Host = HostTextBox.Text?.Trim() ?? "10.1.1.53";
            
            if (int.TryParse(PortTextBox.Text, out var port))
                config.Clover.Port = port;
            
            config.Clover.RemoteAppId = MerchantIdTextBox.Text?.Trim() ?? "";
            config.Clover.SerialNumber = DeviceIdTextBox.Text?.Trim() ?? "";
            config.Clover.AuthToken = TokenTextBox.Text?.Trim() ?? "";
            config.Clover.Secure = SecureCheckBox.IsChecked ?? false;
            
            // Guardar en el archivo
            _configService.UpdateConfig(config);
            
            LogSystem($"💾 Configuración guardada:");
            LogSystem($"   Host: {config.Clover.Host}:{config.Clover.Port}");
            LogSystem($"   Secure (WS/WSS): {(config.Clover.Secure ? "WSS" : "WS")}");
        }
        catch (Exception ex)
        {
            LogSystem($"⚠️ Error guardando configuración: {ex.Message}");
            Log.Error(ex, "Error al guardar configuración");
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
        // Obtener el estado del campo "status"
        string status = "❓ Desconocido";
        string message = "Procesado";
        
        if (data.TryGetProperty("status", out var statusProp))
        {
            var statusText = statusProp.GetString();
            status = statusText switch
            {
                "Successful" => "✅ Exitoso",
                "Cancelled" => "❌ Cancelado",
                "Failed" => "❌ Fallido",
                "InsufficientFunds" => "💳 Sin fondos",
                "Pending" => "⏳ Pendiente",
                _ => "❓ Desconocido"
            };
            
            // Mensaje específico según el estado
            message = statusText switch
            {
                "Successful" => "Transacción completada exitosamente",
                "Cancelled" => "Transacción cancelada",
                "Failed" => "Transacción fallida",
                "InsufficientFunds" => "Fondos insuficientes",
                "Pending" => "Esperando respuesta",
                _ => "Estado desconocido"
            };
        }
        
        // Si hay un mensaje específico en los datos, usarlo
        if (data.TryGetProperty("message", out var msgProp))
        {
            var customMsg = msgProp.GetString();
            if (!string.IsNullOrEmpty(customMsg))
            {
                message = customMsg;
            }
        }
        
        var record = new TransactionRecord
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Type = type,
            Amount = data.TryGetProperty("amount", out var amt) ? $"${amt.GetDecimal() / 100m:F2}" : "N/A",
            TransactionId = data.TryGetProperty("id", out var id) ? id.GetString() ?? "N/A" : "N/A",
            Status = status,
            Message = message
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

            // Validar monto
            if (!TryParsePrice(TestAmountTextBox.Text, out var totalAmount) || totalAmount <= 0)
            {
                MessageBox.Show("Monto inválido. Ingrese un valor mayor a 0", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TestAmountTextBox.Focus();
                return;
            }

            var externalId = string.IsNullOrWhiteSpace(ExternalIdTextBox.Text) ? GenerateExternalId() : ExternalIdTextBox.Text;

            // Crear item de línea simple
            var items = new List<LineItem>
            {
                new LineItem
                {
                    ProductId = "PROD-TEST",
                    ProductName = "Venta de Prueba",
                    Quantity = 1,
                    UnitPrice = totalAmount
                }
            };

            // Crear archivo de transacción con formato simplificado
            var fileService = new TransactionFileService(_configService);
            var notes = $"Venta de prueba - ${totalAmount:F2}";
            
            var transactionFile = fileService.CreateTransactionFile(
                invoiceNumber, 
                externalId, 
                totalAmount,
                customerName: "Cliente de Prueba",
                notes: notes,
                tax: null);

            // Inicializar info de pago con timestamp y timeout de 120 segundos
            transactionFile.PaymentInfo = new PaymentFileInfo
            {
                TotalAmount = totalAmount,
                ProcessingStartTime = DateTime.Now,
                TerminalTimeoutDefault = 120 // 120 segundos
            };

            LogSystem($"📄 Transacción creada: {invoiceNumber}");
            LogSystem($"  � Monto: ${totalAmount:F2}");
            LogSystem($"  ⏱️  Timeout: {transactionFile.PaymentInfo.TerminalTimeoutDefault}s");

            // Guardar a OUTBOX con estado Pending
            transactionFile.Status = TransactionStatus.Pending;
            await fileService.WriteTransactionToOutboxAsync(transactionFile);
            LogSystem($"💾 Archivo guardado en OUTBOX como Pendiente");

            // Enviar pago a Clover con timeout de 120 segundos
            LogSystem($"💳 Enviando pago de ${totalAmount:F2} (Factura: {invoiceNumber})...");
            FooterStatusText.Text = "Procesando pago en terminal...";

            CloverMessage? response = null;
            bool timedOut = false;

            try
            {
                // Enviar transacción con timeout interno de 120s en CloverWebSocketService
                response = await _cloverService.SendSaleAsync(totalAmount, externalId, 0);
                var success = response?.Method?.Contains("RESPONSE") == true;

                if (success)
                {
                    transactionFile.Status = TransactionStatus.Successful;
                    transactionFile.ErrorCode = null;
                    transactionFile.ErrorMessage = null;
                    LogSystem($"✅ Pago aprobado");
                }
                else
                {
                    // Procesar la respuesta desde el WebSocket
                    fileService.ProcessPaymentResult(transactionFile, response);
                    
                    if (transactionFile.Status == TransactionStatus.Cancelled)
                    {
                        LogSystem($"❌ Pago cancelado en terminal");
                    }
                    else if (transactionFile.Status != TransactionStatus.Successful)
                    {
                        LogSystem($"❌ Pago no exitoso: {transactionFile.Status}");
                    }
                }
            }
            catch (TimeoutException timeoutEx)
            {
                // Timeout de 120 segundos alcanzado
                timedOut = true;
                LogSystem($"⏱️  TIMEOUT: Transacción no respondió en 120 segundos");
                
                // Intentar cancelar la transacción en el terminal
                try
                {
                    LogSystem($"🚫 Intentando cancelar transacción en terminal...");
                    await _cloverService.CancelTransactionAsync();
                    LogSystem($"✅ Comando de cancelación enviado al terminal");
                }
                catch (Exception cancelEx)
                {
                    LogSystem($"⚠️  No se pudo cancelar en terminal: {cancelEx.Message}");
                }
                
                LogSystem($"🔍 Consultando OUTBOX para verificar estado final...");

                // Consultar OUTBOX por si el InboxWatcher procesó la transacción
                await Task.Delay(2000); // Esperar 2 segundos por si hay escritura pendiente
                var outboxTransaction = await fileService.ReadLatestTransactionFromOutboxAsync(invoiceNumber);

                if (outboxTransaction != null && outboxTransaction.Status != TransactionStatus.Pending)
                {
                    // Encontramos un resultado en OUTBOX
                    LogSystem($"✅ Estado encontrado en OUTBOX: {outboxTransaction.Status}");
                    transactionFile = outboxTransaction;
                }
                else
                {
                    // No hay resultado, marcar como cancelado por timeout
                    LogSystem($"❌ No se encontró resultado en OUTBOX, marcando como Cancelado");
                    transactionFile.Status = TransactionStatus.Cancelled;
                    transactionFile.ErrorCode = "TIMEOUT";
                    transactionFile.ErrorMessage = "Timeout después de 120 segundos - transacción cancelada";
                    
                    if (transactionFile.PaymentInfo != null)
                    {
                        transactionFile.PaymentInfo.TimeoutSeconds = 120;
                        transactionFile.PaymentInfo.CancelledReason = "Timeout de 120 segundos";
                        transactionFile.PaymentInfo.CancelledTimestamp = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                // Otro error
                LogSystem($"❌ Error en transacción: {ex.Message}");
                transactionFile.Status = TransactionStatus.Failed;
                transactionFile.ErrorCode = "ERROR";
                transactionFile.ErrorMessage = ex.Message;
            }

            // Guardar resultado actualizado a OUTBOX
            await fileService.WriteTransactionToOutboxAsync(transactionFile);
            LogSystem($"💾 Resultado guardado: {transactionFile.Status}");

            if (response != null)
            {
                var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                LogSystem($"📥 Respuesta:\n{responseJson}");
            }
            else if (timedOut)
            {
                LogSystem($"⏱️  Sin respuesta del terminal (timeout)");
            }

            // Registrar en transacciones - crear JsonElement con datos de la transacción
            var transactionMessage = transactionFile.Status switch
            {
                TransactionStatus.Successful => "Transacción completada exitosamente",
                TransactionStatus.Cancelled => transactionFile.ErrorMessage ?? "Transacción cancelada",
                TransactionStatus.Timeout => "Timeout - Sin respuesta del terminal",
                TransactionStatus.Failed => transactionFile.ErrorMessage ?? "Transacción fallida",
                TransactionStatus.InsufficientFunds => "Fondos insuficientes",
                TransactionStatus.Pending => "Esperando respuesta",
                _ => "Estado desconocido"
            };
            
            // NO agregar aquí - el monitor de OUTBOX lo hará automáticamente
            // Solo actualizar métricas si es exitoso
            if (transactionFile.Status == TransactionStatus.Successful)
            {
                _totalPayments++;
                _totalPaymentsAmount += totalAmount;
                UpdateMetrics();
            }
            else if (transactionFile.Status == TransactionStatus.Failed || 
                     transactionFile.Status == TransactionStatus.Cancelled ||
                     transactionFile.Status == TransactionStatus.Timeout)
            {
                _totalErrors++;
                UpdateMetrics();
            }

            // Reiniciar formulario
            ExternalIdTextBox.Text = GenerateExternalId();
            InvoiceNumberTextBox.Clear();

            // Actualizar estado en footer
            FooterStatusText.Text = transactionFile.Status switch
            {
                TransactionStatus.Successful => "✅ Pago procesado exitosamente",
                TransactionStatus.Cancelled => "❌ Pago cancelado",
                TransactionStatus.Timeout => "⏱️ Timeout - Sin respuesta",
                TransactionStatus.Failed => "❌ Pago rechazado",
                TransactionStatus.InsufficientFunds => "💳 Sin fondos",
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

    // ============ Testing Helper Methods ============

    private void GenerateInvoiceNumber_Click(object sender, RoutedEventArgs e)
    {
        GenerateNewInvoiceNumber();
    }

    private void GenerateNewInvoiceNumber()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(1000, 9999);
        InvoiceNumberTextBox.Text = $"FB-{timestamp}-{random}";
        LogSystem($"🎲 Nuevo número de factura: {InvoiceNumberTextBox.Text}");
    }

    private void TestAmount_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Evitar NullReferenceException durante InitializeComponent
        if (TestAmountTextBox == null || TestAmountPreview == null)
            return;
            
        try
        {
            bool TryParsePrice(string text, out decimal value)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0;
                    return false;
                }
                var normalized = text.Replace(",", ".");
                return decimal.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            if (TryParsePrice(TestAmountTextBox.Text, out var amount))
            {
                TestAmountPreview.Text = $"Monto a enviar: ${amount:F2}";
                TestAmountPreview.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            }
            else
            {
                TestAmountPreview.Text = "Monto inválido";
                TestAmountPreview.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            }
        }
        catch
        {
            if (TestAmountPreview != null)
                TestAmountPreview.Text = "Error";
        }
    }

    // Métodos antiguos eliminados - ya no se usan productos individuales

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
            // Usar el método centralizado para guardar
            SaveConfiguration();

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
        // Siempre guardar configuración antes de cerrar/ocultar
        SaveConfiguration();
        
        if (!ForceClose && !_isExiting)
        {
            e.Cancel = true;
            this.Hide();
            LogSystem("➖ Aplicación minimizada al systray");
        }
        else
        {
            _uptimeTimer.Stop();
            // _outboxMonitorTimer.Stop(); // ELIMINADO
            _timeoutCounterTimer.Stop();
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


    // ============ OUTBOX Management Methods - ELIMINADOS ============
    // Los métodos de gestión de OUTBOX se han eliminado.
    // Ahora se usa doble click en la tabla de transacciones para ver detalles.

    private void RefreshOutboxButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa
        try
        {
            var fileService = new TransactionFileService(_configService);
            var config = _configService.GetConfig();
            var outboxPath = config.Folders.Outbox;

            // OutboxFileListBox.Items.Clear();

            if (Directory.Exists(outboxPath))
            {
                var files = Directory.GetFiles(outboxPath, "*.json")
                    .Select(Path.GetFileName)
                    .OrderByDescending(f => f)
                    .ToList();

                // foreach (var file in files)
                // {
                //     OutboxFileListBox.Items.Add(file);
                // }

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

    private void OutboxFileListBox_SelectionChanged_Removed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // MÉTODO ELIMINADO
    }

    private void LoadOutboxFileDetails_Removed(string filename)
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

                // OutboxDetailsTextBox.Text = formatted; // ELIMINADO
                LogSystem($"📄 Archivo cargado: {filename}");
            }
        }
        catch (Exception ex)
        {
            // OutboxDetailsTextBox.Text = $"Error loading file: {ex.Message}"; // ELIMINADO
            LogSystem($"❌ Error loading file: {ex.Message}");
        }
    }

    private void ViewOutboxDetailsButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO
    }

    private async void ApproveTransactionButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa
    }

    private async void RejectTransactionButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa
    }

    private async void ArchiveTransactionButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa
    }

    private async void CleanupInboxButton_Click_Removed(object sender, RoutedEventArgs e)
    {
        // MÉTODO ELIMINADO - Ya no se usa
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
