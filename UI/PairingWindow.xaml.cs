using CloverBridge.Models;
using CloverBridge.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace CloverBridge.UI;

public partial class PairingWindow : Window
{
    private readonly CloverWebSocketService _cloverService;

    public PairingWindow(CloverWebSocketService cloverService)
    {
        InitializeComponent();
        _cloverService = cloverService;

        // Suscribirse a cambios de estado
        _cloverService.StateChanged += OnStateChanged;

        // Mostrar estado actual
        UpdateStatus(_cloverService.State);

        // Si ya hay un código, mostrarlo
        if (!string.IsNullOrEmpty(_cloverService.LastPairingCode))
        {
            UpdatePairingCode(_cloverService.LastPairingCode);
        }
    }

    private void OnStateChanged(object? sender, ConnectionState state)
    {
        Dispatcher.Invoke(() => UpdateStatus(state));
    }

    private void UpdateStatus(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.Disconnected:
                StatusText.Text = "Desconectado del terminal";
                CodeBorder.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                break;

            case ConnectionState.Connecting:
                StatusText.Text = "Conectando al terminal...";
                CodeBorder.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                break;

            case ConnectionState.Connected:
                StatusText.Text = "Conectado, esperando pairing...";
                CodeBorder.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                break;

            case ConnectionState.PairingRequired:
                StatusText.Text = "Pairing requerido";
                break;

            case ConnectionState.Paired:
                StatusText.Text = "✓ Pareado exitosamente";
                StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                CodeBorder.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                
                // Cerrar automáticamente después de 3 segundos
                Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(Close);
                });
                break;

            case ConnectionState.Error:
                StatusText.Text = "Error de conexión";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                CodeBorder.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                break;
        }
    }

    public void UpdatePairingCode(string code)
    {
        Dispatcher.Invoke(() =>
        {
            PairingCodeText.Text = code;
            CodeBorder.Visibility = Visibility.Visible;
            InstructionsText.Visibility = Visibility.Visible;
            StatusText.Text = "Código de pairing recibido";
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cloverService.StateChanged -= OnStateChanged;
        base.OnClosed(e);
    }
}
