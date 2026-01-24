using CloverBridge.Services;
using Serilog;
using System.Windows;

namespace CloverBridge.UI;

public partial class ConfigWindow : Window
{
    private readonly ConfigurationService _configService;

    public ConfigWindow(ConfigurationService configService)
    {
        InitializeComponent();
        _configService = configService;
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.GetConfig();
        HostTextBox.Text = config.Clover.Host;
        PortTextBox.Text = config.Clover.Port.ToString();
        MerchantIdTextBox.Text = config.Clover.RemoteAppId;
        DeviceIdTextBox.Text = config.Clover.SerialNumber;
        TokenTextBox.Text = config.Clover.AuthToken;
        SecureCheckBox.IsChecked = config.Clover.Secure;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.GetConfig();
        config.Clover.Host = HostTextBox.Text.Trim();
        if (int.TryParse(PortTextBox.Text, out var port))
            config.Clover.Port = port;
        config.Clover.RemoteAppId = MerchantIdTextBox.Text.Trim();
        config.Clover.SerialNumber = DeviceIdTextBox.Text.Trim();
        
        var uiToken = TokenTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(uiToken))
        {
            config.Clover.AuthToken = uiToken;
        }
        
        config.Clover.Secure = SecureCheckBox.IsChecked ?? false;

        _configService.UpdateConfig(config);
        Log.Information("💾 Configuración guardada exitosamente. Secure: {Secure}", config.Clover.Secure);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
