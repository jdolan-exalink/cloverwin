using CloverBridge.Services;
using Serilog;
using System.Windows;

namespace CloverBridge.UI;

public partial class ConfigWindow : Window
{
    private readonly ConfigurationService _configService;
    private readonly MercadoPagoService? _mpService;

    public ConfigWindow(ConfigurationService configService, MercadoPagoService? mpService = null)
    {
        InitializeComponent();
        _configService = configService;
        _mpService = mpService ?? new MercadoPagoService(configService);
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.GetConfig();
        
        // General
        DefaultProviderComboBox.SelectedIndex = config.PaymentProvider == "QRMP" ? 1 : 0;

        // Clover
        CloverEnabledCheckBox.IsChecked = config.Clover.Enabled;
        HostTextBox.Text = config.Clover.Host;
        PortTextBox.Text = config.Clover.Port.ToString();
        MerchantIdTextBox.Text = config.Clover.RemoteAppId;
        DeviceIdTextBox.Text = config.Clover.SerialNumber;
        TokenTextBox.Text = config.Clover.AuthToken;
        SecureCheckBox.IsChecked = config.Clover.Secure;

        // QRMP
        MpEnabledCheckBox.IsChecked = config.Qrmp.Enabled;
        MpTokenTextBox.Text = config.Qrmp.AccessToken;
        MpUserIdTextBox.Text = config.Qrmp.UserId.ToString();
        MpStoreIdTextBox.Text = config.Qrmp.ExternalStoreId;
        MpPosIdTextBox.Text = config.Qrmp.ExternalPosId;
        MpWebhookUrlTextBox.Text = config.Qrmp.WebhookUrl;
        MpWebhookSecretTextBox.Text = config.Qrmp.WebhookSecret;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.GetConfig();
        
        // General
        config.PaymentProvider = (DefaultProviderComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "CLOVER";

        // Clover
        config.Clover.Enabled = CloverEnabledCheckBox.IsChecked ?? true;
        config.Clover.Host = HostTextBox.Text.Trim();
        if (int.TryParse(PortTextBox.Text, out var port))
            config.Clover.Port = port;
        config.Clover.RemoteAppId = MerchantIdTextBox.Text.Trim();
        config.Clover.SerialNumber = DeviceIdTextBox.Text.Trim();
        config.Clover.AuthToken = TokenTextBox.Text.Trim();
        config.Clover.Secure = SecureCheckBox.IsChecked ?? false;

        // QRMP
        config.Qrmp.Enabled = MpEnabledCheckBox.IsChecked ?? true;
        config.Qrmp.AccessToken = MpTokenTextBox.Text.Trim();
        if (long.TryParse(MpUserIdTextBox.Text, out var userId))
            config.Qrmp.UserId = userId;
        config.Qrmp.ExternalStoreId = MpStoreIdTextBox.Text.Trim();
        config.Qrmp.ExternalPosId = MpPosIdTextBox.Text.Trim();
        config.Qrmp.WebhookUrl = MpWebhookUrlTextBox.Text.Trim();
        config.Qrmp.WebhookSecret = MpWebhookSecretTextBox.Text.Trim();

        _configService.UpdateConfig(config);
        Log.Information("💾 Configuración guardada. Proveedor default: {Provider}", config.PaymentProvider);
        
        DialogResult = true;
        Close();
    }

    private async void TestMp_Click(object sender, RoutedEventArgs e)
    {
        if (_mpService == null) return;

        // Guardar temporalmente para el test
        var config = _configService.GetConfig();
        config.Qrmp.AccessToken = MpTokenTextBox.Text.Trim();
        if (long.TryParse(MpUserIdTextBox.Text, out var userId)) config.Qrmp.UserId = userId;
        config.Qrmp.ExternalStoreId = MpStoreIdTextBox.Text.Trim();

        MessageBox.Show("Probando credenciales de Mercado Pago...", "Test MP", MessageBoxButton.OK, MessageBoxImage.Information);
        
        var success = await _mpService.TestCredentialsAsync();
        
        if (success)
            MessageBox.Show("✅ Credenciales válidas. Sucursal encontrada.", "Test MP", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show("❌ Credenciales inválidas o sucursal no encontrada.", "Test MP", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
