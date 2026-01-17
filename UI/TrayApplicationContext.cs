using CloverBridge.Models;
using CloverBridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CloverBridge.UI;

/// <summary>
/// System Tray application context
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IHost _host;
    private readonly ConfigurationService _configService;
    private readonly CloverWebSocketService _cloverService;
    private readonly ApiService _apiService;
    private readonly TransactionQueueService _queueService;
    private readonly InboxWatcherService _inboxService;
    private PairingWindow? _pairingWindow;
    private ProductionMainWindow? _mainWindow;

    public TrayApplicationContext()
    {
        // Crear host de servicios
        var builder = Host.CreateDefaultBuilder();
        builder.UseSerilog();
        
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<CloverWebSocketService>();
            services.AddSingleton<TransactionQueueService>();
            services.AddSingleton<InboxWatcherService>();
            services.AddSingleton<ApiService>();

            services.AddHostedService<CloverWebSocketService>(sp => sp.GetRequiredService<CloverWebSocketService>());
            services.AddHostedService<TransactionQueueService>(sp => sp.GetRequiredService<TransactionQueueService>());
            services.AddHostedService<InboxWatcherService>(sp => sp.GetRequiredService<InboxWatcherService>());
            services.AddHostedService<ApiService>(sp => sp.GetRequiredService<ApiService>());
        });

        _host = builder.Build();

        // Obtener servicios
        _configService = _host.Services.GetRequiredService<ConfigurationService>();
        _cloverService = _host.Services.GetRequiredService<CloverWebSocketService>();
        _apiService = _host.Services.GetRequiredService<ApiService>();
        _queueService = _host.Services.GetRequiredService<TransactionQueueService>();
        _inboxService = _host.Services.GetRequiredService<InboxWatcherService>();

        // Suscribirse a eventos de Clover
        _cloverService.StateChanged += OnCloverStateChanged;
        _cloverService.PairingCodeReceived += OnPairingCodeReceived;

        // Crear icono de bandeja con tarjeta de crédito
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIconFromEmoji("💳"),
            Text = "CloverBridge - Sistema de Pagos",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.DoubleClick += OnTrayDoubleClick;

        // Iniciar servicios
        _ = _host.StartAsync();

        Log.Information("TrayApplicationContext initialized");
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("CloverBridge v1.0")
        {
            Enabled = false,
            Font = new Font(menu.Font, FontStyle.Bold)
        };
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());

        var openUIItem = new ToolStripMenuItem("📊 Abrir Panel de Control")
        {
            Font = new Font(menu.Font, FontStyle.Bold)
        };
        openUIItem.Click += (s, e) => OpenMainWindow();
        menu.Items.Add(openUIItem);

        var openDashboardItem = new ToolStripMenuItem("Abrir Dashboard");
        openDashboardItem.Click += (s, e) =>
        {
            try
            {
                var config = _configService.GetConfig();
                var url = $"http://{config.Api.Host}:{config.Api.Port}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening dashboard");
                MessageBox.Show($"Error abriendo dashboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        openDashboardItem.Click += (s, e) => OpenDashboard();
        menu.Items.Add(openDashboardItem);

        var showPairingItem = new ToolStripMenuItem("Mostrar Código de Pairing");
        showPairingItem.Click += (s, e) => ShowPairingWindow();
        menu.Items.Add(showPairingItem);

        menu.Items.Add(new ToolStripSeparator());

        var configItem = new ToolStripMenuItem("Configuración");
        configItem.Click += (s, e) => OpenConfiguration();
        menu.Items.Add(configItem);

        var logsItem = new ToolStripMenuItem("Ver Logs");
        logsItem.Click += (s, e) => OpenLogs();
        menu.Items.Add(logsItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Cerrar Aplicación")
        {
            Font = new Font(menu.Font, FontStyle.Bold),
            ForeColor = Color.Red
        };
        exitItem.Click += async (s, e) =>
        {
            var result = MessageBox.Show(
                "¿Está seguro que desea cerrar CloverBridge?\\n\\nEsto detendrá todos los servicios.",
                "Confirmar Salida",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (result == DialogResult.Yes)
            {
                Log.Information("User requested application shutdown");
                _notifyIcon.Visible = false;
                
                try
                {
                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                }
                catch { }
                
                // Forzar cierre de ventanas
                if (_mainWindow != null)
                {
                    _mainWindow.ForceClose = true;
                    _mainWindow.Close();
                }
                _pairingWindow?.Close();
                
                Application.Exit();
                Environment.Exit(0);
            }
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private Icon CreateIconFromEmoji(string emoji)
    {
        try
        {
            // Crear un Bitmap de 16x16 píxeles (tamaño estándar del icono de bandeja)
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                
                // Usar una fuente más grande y luego escalarla
                using (var font = new Font("Segoe UI Emoji", 12, FontStyle.Regular))
                {
                    var textSize = g.MeasureString(emoji, font);
                    var x = (bitmap.Width - textSize.Width) / 2;
                    var y = (bitmap.Height - textSize.Height) / 2;
                    g.DrawString(emoji, font, Brushes.White, x, y);
                }
            }

            // Convertir Bitmap a Icon
            var handle = bitmap.GetHicon();
            var icon = Icon.FromHandle(handle);
            return icon;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error creating emoji icon, using default");
            return SystemIcons.Application;
        }
    }

    private void ExitApplication()
    {
        Log.Information("Exiting application from tray");
        
        // Cerrar ventana principal forzadamente si existe
        if (_mainWindow != null)
        {
            _mainWindow.ForceClose = true;
            _mainWindow.Close();
        }
        
        // Cerrar ventana de pairing si existe
        _pairingWindow?.Close();
        
        // Detener servicios
        _host.StopAsync().Wait();
        _host.Dispose();
        
        // Ocultar y liberar icono de bandeja
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        
        ExitThread();
    }

    private void OnCloverStateChanged(object? sender, ConnectionState state)
    {
        var tooltip = $"CloverBridge - {state switch
        {
            ConnectionState.Connected => "Conectado",
            ConnectionState.Paired => "Pareado",
            ConnectionState.PairingRequired => "Pairing requerido",
            ConnectionState.Connecting => "Conectando...",
            ConnectionState.Disconnected => "Desconectado",
            ConnectionState.Error => "Error",
            _ => "Desconocido"
        }}";

        _notifyIcon.Text = tooltip;

        Log.Information("Tray tooltip updated: {Tooltip}", tooltip);
    }

    private void OnPairingCodeReceived(object? sender, string code)
    {
        Log.Information("Pairing code received in tray: {Code}", code);
        
        if (_pairingWindow == null || !_pairingWindow.IsVisible)
        {
            ShowPairingWindow();
        }

        _pairingWindow?.UpdatePairingCode(code);
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsVisible)
        {
            if (_mainWindow == null)
            {
                _mainWindow = new ProductionMainWindow(
                    _cloverService,
                    _configService,
                    _queueService,
                    _inboxService
                );
                _mainWindow.Closed += (s, e) => _mainWindow = null;
            }
            _mainWindow.ShowWindow();
        }
        else
        {
            _mainWindow.ShowWindow();
        }
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

    private void OpenDashboard()
    {
        try
        {
            var config = _configService.GetConfig();
            var url = $"http://{config.Api.Host}:{config.Api.Port}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening dashboard");
            MessageBox.Show("No se pudo abrir el dashboard", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenConfiguration()
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
            Log.Error(ex, "Error opening configuration folder");
        }
    }

    private void OpenLogs()
    {
        try
        {
            var logsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CloverBridge",
                "logs"
            );
            Process.Start("explorer.exe", logsPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening logs folder");
        }
    }

    private void Exit()
    {
        Log.Information("Exiting application");
        
        _notifyIcon.Visible = false;
        _pairingWindow?.Close();
        
        _ = _host.StopAsync();
        _host.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon?.Dispose();
            if (_pairingWindow != null && _pairingWindow.IsVisible)
            {
                _pairingWindow.Close();
            }
            _host?.Dispose();
        }
        base.Dispose(disposing);
    }
}
