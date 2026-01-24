using CloverBridge.Models;
using CloverBridge.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CloverBridge.UI;

public partial class MainWindow : Window
{
    private readonly CloverWebSocketService _cloverService;
    private readonly ConfigurationService _configService;
    private readonly TransactionQueueService _queueService;
    private readonly InboxWatcherService _inboxService;
    private PairingWindow? _pairingWindow;
    private bool _isExiting = false;
    
    public bool ForceClose { get; set; } = false;

    public MainWindow(
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

        // Mostrar versión
        VersionTextBlock.Text = AppVersion.GetVersion();

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

        LogSystem("🚀 CloverBridge UI iniciado");
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configService.GetConfig();
            HostTextBox.Text = config.Clover.Host ?? "10.1.1.53";
            PortTextBox.Text = config.Clover.Port.ToString();
            MerchantIdTextBox.Text = config.Clover.RemoteAppId ?? "";
            DeviceIdTextBox.Text = config.Clover.SerialNumber ?? "";
            TokenTextBox.Text = config.Clover.AuthToken ?? "";
            SecureCheckBox.IsChecked = config.Clover.Secure;
            
            // Mostrar configuración actual en el log
            LogSystem($"⚙️ Configuración cargada:");
            LogSystem($"   Host: {config.Clover.Host}:{config.Clover.Port}");
            LogSystem($"   Secure: {config.Clover.Secure}");
            LogSystem($"   Remote App ID: {config.Clover.RemoteAppId}");
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
            LogSystem($"   Remote App ID: {config.Clover.RemoteAppId}");
            LogSystem($"   Serial Number: {config.Clover.SerialNumber}");
            LogSystem($"   Auth Token: {(string.IsNullOrEmpty(config.Clover.AuthToken) ? "(vacío)" : "***" + config.Clover.AuthToken.Substring(Math.Max(0, config.Clover.AuthToken.Length - 4)))}");
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
            ConnectionState.Connected => ("Conectado", "#00ff00", "#004400"),
            ConnectionState.Paired => ("Pareado", "#00ff00", "#004400"),
            ConnectionState.PairingRequired => ("Pairing Requerido", "#ffa500", "#664400"),
            ConnectionState.Connecting => ("Conectando...", "#ffff00", "#666600"),
            ConnectionState.Disconnected => ("Desconectado", "#ff4444", "#440000"),
            ConnectionState.Error => ("Error de Conexión", "#ff0000", "#440000"),
            _ => ("Desconocido", "#888888", "#333333")
        };

        ConnectionStatusText.Text = statusInfo.Item1;
        ConnectionStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusInfo.Item2));
        ConnectionStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusInfo.Item3));

        if (state == ConnectionState.Error)
        {
            var config = _configService.GetConfig();
            LogSystem($"📡 Estado de conexión: {statusInfo.Item1}");
            LogSystem($"❌ No se puede conectar a {config.Clover.Host}:{config.Clover.Port}");
            LogSystem($"   Verifique que:");
            LogSystem($"   1. El terminal Clover esté encendido");
            LogSystem($"   2. Esté en la misma red que este PC");
            LogSystem($"   3. Network Pay Display esté habilitado");
            LogSystem($"   4. La IP y puerto sean correctos");
        }
        else if (state == ConnectionState.Paired)
        {
            LogSystem($"📡 Estado de conexión: {statusInfo.Item1}");
            LogSystem("✅ Pairing completado exitosamente!");
            
            // Cerrar popup de pairing si está abierto
            PairingPopup.Visibility = Visibility.Collapsed;
            FooterStatusText.Text = "Pairing completado";
        }
        else
        {
            LogSystem($"📡 Estado de conexión: {statusInfo.Item1}");
        }
    }

    private void OnPairingCodeReceived(object? sender, string code)
    {
        Dispatcher.Invoke(() =>
        {
            LogSystem($"🔐 Código de pairing recibido: {code}");
            ShowPairingPopup(code);
        });
    }

    private void OnCloverMessageReceived(object? sender, CloverMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
                LogResponse($"📨 Respuesta recibida:\n{json}");
            }
            catch (Exception ex)
            {
                LogResponse($"📨 Mensaje recibido: {message.Method}");
                LogSystem($"Error serializando mensaje: {ex.Message}");
            }
        });
    }

    // Métodos de testing removidos - solo Nueva Venta disponible
    /*
    private async void GenerateQRButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(QRAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"🎫 Generando QR por ${amount}...");
            FooterStatusText.Text = "Generando QR...";

            var request = new
            {
                type = "qr",
                amount = amount,
                externalId = GenerateExternalId(),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            
            // Obtener carpeta inbox de configuración
            var config = _configService.GetConfig();
            var inboxPath = config.Folders.Inbox;

            Directory.CreateDirectory(inboxPath);
            var filename = $"qr_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var filepath = Path.Combine(inboxPath, filename);
            await File.WriteAllTextAsync(filepath, json);

            LogSystem($"✅ Solicitud QR creada: {filename}");
            LogSystem($"   📁 Guardada en: {inboxPath}");
            LogResponse($"📤 Solicitud enviada:\n{json}");

            // Mostrar en UI (simulado)
            QRCodeText.Text = $"QR: ${amount}\nID: {request.externalId}\nArchivo: {filename}";
            QRDisplayBorder.Visibility = Visibility.Visible;

            FooterStatusText.Text = "QR generado exitosamente";
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error generando QR: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error generando QR";
        }
    }
    */

    private async void SendSaleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(SaleAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var externalId = string.IsNullOrWhiteSpace(ExternalIdTextBox.Text) 
                ? GenerateExternalId() 
                : ExternalIdTextBox.Text;

            // Verificar estado de conexión
            if (_cloverService.State != ConnectionState.Paired)
            {
                MessageBox.Show("Debe estar conectado y pareado para enviar ventas", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"💳 Enviando venta por ${amount}...");
            FooterStatusText.Text = "Procesando venta...";

            // Enviar venta directamente a través del WebSocket (igual que TypeScript)
            var response = await _cloverService.SendSaleAsync(amount, externalId, 0);

            LogSystem($"✅ Respuesta recibida del terminal");
            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            LogResponse($"📥 Respuesta:\n{responseJson}");

            // Generar nuevo External ID para la próxima transacción
            ExternalIdTextBox.Text = GenerateExternalId();

            FooterStatusText.Text = "Venta procesada exitosamente";
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error enviando venta: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error procesando venta";
        }
    }

    /*
    private async void SendAuthButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(AuthAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"🔐 Enviando autorización por ${amount}...");
            FooterStatusText.Text = "Procesando autorización...";

            var request = new
            {
                type = "auth",
                amount = amount,
                externalId = GenerateExternalId(),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            
            // Obtener carpeta inbox de configuración
            var config = _configService.GetConfig();
            var inboxPath = config.Folders.Inbox;

            Directory.CreateDirectory(inboxPath);
            var filename = $"auth_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var filepath = Path.Combine(inboxPath, filename);
            await File.WriteAllTextAsync(filepath, json);

            LogSystem($"✅ Solicitud de autorización creada: {filename}");
            LogSystem($"   📁 Guardada en: {inboxPath}");
            LogResponse($"📤 Solicitud enviada:\n{json}");

            FooterStatusText.Text = "Autorización enviada exitosamente";
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error enviando autorización: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error procesando autorización";
        }
    }
    */

    /*
    private async void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PaymentIdTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese un Payment ID", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"❌ Anulando pago: {PaymentIdTextBox.Text}...");
            FooterStatusText.Text = "Procesando anulación...";

            var request = new
            {
                type = "void",
                paymentId = PaymentIdTextBox.Text,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            
            var inboxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CloverBridge",
                "inbox"
            );

            Directory.CreateDirectory(inboxPath);
            var filename = $"void_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var filepath = Path.Combine(inboxPath, filename);
            await File.WriteAllTextAsync(filepath, json);

            LogSystem($"✅ Solicitud de anulación creada: {filename}");
            LogResponse($"📤 Solicitud enviada:\n{json}");

            FooterStatusText.Text = "Anulación enviada exitosamente";
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error anulando pago: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error procesando anulación";
        }
    }
    */

    /*
    private async void RefundButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PaymentIdTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese un Payment ID", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(AuthAmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Por favor ingrese un monto válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verificar estado de conexión
            if (_cloverService.State != ConnectionState.Paired)
            {
                MessageBox.Show("Debe estar conectado y pareado para enviar reembolsos", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogSystem($"↩️ Devolviendo pago: {PaymentIdTextBox.Text}...");
            FooterStatusText.Text = "Procesando devolución...";

            // Enviar refund directamente a través del WebSocket (igual que TypeScript)
            var response = await _cloverService.SendRefundAsync(amount, PaymentIdTextBox.Text, null, false);

            LogSystem($"✅ Respuesta recibida del terminal");
            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            LogResponse($"📥 Respuesta:\n{responseJson}");

            FooterStatusText.Text = "Devolución procesada exitosamente";
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error devolviendo pago: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FooterStatusText.Text = "Error procesando devolución";
        }
    }
    */

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Usar el método centralizado para guardar
            SaveConfiguration();
            
            MessageBox.Show("Configuración guardada. Reinicie la aplicación para aplicar cambios.", 
                          "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error guardando configuración: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenConfigFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CloverBridge"
            );
            Process.Start("explorer.exe", appDataPath);
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error abriendo carpeta: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PairingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogSystem("🔐 Iniciando proceso de pairing...");
            
            // Si ya tenemos un código, mostrarlo
            if (!string.IsNullOrEmpty(_cloverService.LastPairingCode))
            {
                ShowPairingPopup(_cloverService.LastPairingCode);
            }
            else
            {
                // Forzar nuevo pairing
                _ = ForcePairingAsync();
            }
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error iniciando pairing: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ForcePairingAsync()
    {
        try
        {
            LogSystem("🔄 Forzando nuevo pairing...");
            FooterStatusText.Text = "Forzando pairing...";
            
            // Reconnect forzará un nuevo pairing
            await _cloverService.DisconnectAsync();
            await Task.Delay(1000);
            
            // Iniciar servicio nuevamente (ConnectAsync se llama internamente)
            var config = _configService.GetConfig();
            LogSystem($"📡 Reconectando a {config.Clover.Host}:{config.Clover.Port}...");
            
            LogSystem("✅ Solicitud de pairing enviada");
        }
        catch (Exception ex)
        {
            LogSystem($"❌ Error forzando pairing: {ex.Message}");
            FooterStatusText.Text = "Error forzando pairing";
        }
    }

    private void ShowPairingPopup(string code)
    {
        PopupPairingCode.Text = code;
        PopupPairingStatus.Text = "Ingresa este código en tu terminal Clover";
        PairingPopup.Visibility = Visibility.Visible;
        
        LogSystem($"💡 Popup de pairing mostrado con código: {code}");
    }

    private void ClosePairingPopup_Click(object sender, RoutedEventArgs e)
    {
        PairingPopup.Visibility = Visibility.Collapsed;
        LogSystem("❌ Popup de pairing cerrado");
    }

    private async void RetryPairingButton_Click(object sender, RoutedEventArgs e)
    {
        PairingPopup.Visibility = Visibility.Collapsed;
        await ForcePairingAsync();
    }

    private void PairingPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Cerrar al hacer click en el overlay
        if (e.OriginalSource == PairingPopup)
        {
            PairingPopup.Visibility = Visibility.Collapsed;
        }
    }

    private void PairingPopupContent_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Evitar que el click en el contenido cierre el popup
        e.Handled = true;
    }

    private void ShowPairingWindow()
    {
        if (_pairingWindow == null || !_pairingWindow.IsVisible)
        {
            _pairingWindow = new PairingWindow(_cloverService);
            _pairingWindow.Show();
        }
        else
        {
            _pairingWindow.Activate();
        }
    }

    /*
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        LogSystem("🔽 Ventana minimizada a bandeja del sistema");
    }
    */

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Siempre guardar configuración antes de cerrar/ocultar
        SaveConfiguration();
        
        // Si ForceClose está activado, permitir cierre real
        if (ForceClose)
        {
            base.OnClosing(e);
            return;
        }
        
        // Prevenir cierre de la ventana, solo ocultarla
        e.Cancel = true;
        Hide();
        LogSystem("💤 Ventana ocultada. Usar systray para cerrar la aplicación.");
    }

    public void ShowMainWindow()
    {
        Show();
        WindowState = System.Windows.WindowState.Normal;
        Activate();
    }

    private void ClearResponseButton_Click(object sender, RoutedEventArgs e)
    {
        ResponseLogTextBox.Clear();
        LogSystem("🗑️ Log de respuestas limpiado");
    }

    private void ClearSystemButton_Click(object sender, RoutedEventArgs e)
    {
        SystemLogTextBox.Clear();
    }

    private void LogResponse(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        ResponseLogTextBox.AppendText($"[{timestamp}] {message}\n\n");
        ResponseLogTextBox.ScrollToEnd();
    }

    private void LogSystem(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        SystemLogTextBox.AppendText($"[{timestamp}] {message}\n");
        SystemLogTextBox.ScrollToEnd();
        
        Log.Information(message.Replace("🚀", "").Replace("✅", "").Replace("❌", "")
            .Replace("📡", "").Replace("🔐", "").Replace("💳", "").Replace("🎫", "")
            .Replace("↩️", "").Replace("🗑️", "").Replace("🔽", "").Trim());
    }

    private string GenerateExternalId()
    {
        return $"EXT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 32);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Si no es un cierre forzado, solo ocultar la ventana
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            LogSystem("🔽 Ventana oculta. Usa el icono de la bandeja para volver a abrirla.");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _cloverService.StateChanged -= OnCloverStateChanged;
        _cloverService.PairingCodeReceived -= OnPairingCodeReceived;
        _cloverService.MessageReceived -= OnCloverMessageReceived;
    }
}
