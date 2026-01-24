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
    private readonly MercadoPagoService _mpService;
    private PairingWindow? _pairingWindow;
    private ProductionMainWindow? _mainWindow;
    private bool _initialCheckPerformed = false;

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
            services.AddSingleton<MercadoPagoService>();

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
        _mpService = _host.Services.GetRequiredService<MercadoPagoService>();

        // Suscribirse a eventos de Clover
        _cloverService.StateChanged += OnCloverStateChanged;
        _cloverService.PairingCodeReceived += OnPairingCodeReceived;

        // Intentar cargar el icono desde el archivo .ico comercial
        Icon trayIcon;
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var appPath = !string.IsNullOrEmpty(exePath) 
                ? Path.GetDirectoryName(exePath) 
                : AppContext.BaseDirectory;

            var iconPath = Path.Combine(appPath ?? "", "cloverwin.ico");
            if (File.Exists(iconPath))
            {
                trayIcon = new Icon(iconPath);
            }
            else
            {
                trayIcon = CreateIconFromEmoji("💳");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo cargar cloverwin.ico, usando genérico");
            trayIcon = SystemIcons.Application;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "CloverBridge - Sistema de Pagos",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.DoubleClick += OnTrayDoubleClick;

        // Iniciar servicios
        _ = _host.StartAsync();

        Log.Information("TrayApplicationContext initialized");

        // Chequeo de inicio: Si en 10 segundos no está integrado, mostrar la ventana principal
        // Usamos un Timer de WinForms para que se ejecute en el hilo de la UI correctamente
        var startupTimer = new System.Windows.Forms.Timer();
        startupTimer.Interval = 10000;
        startupTimer.Tick += (s, e) => {
            startupTimer.Stop();
            if (_cloverService.State != ConnectionState.Paired && !_initialCheckPerformed)
            {
                Log.Information("Auto-integration check: Terminal not paired after timeout, showing main window.");
                OpenMainWindow();
            }
        };
        startupTimer.Start();
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

        var logsItem = new ToolStripMenuItem("Ver Logs en Tiempo Real");
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
                Log.Information("User requested application shutdown via tray");
                _notifyIcon.Visible = false;
                
                // Forzar cierre de ventanas primero
                if (_mainWindow != null)
                {
                    _mainWindow.ForceClose = true;
                    _mainWindow.Dispatcher.Invoke(() => _mainWindow.Close());
                }
                
                _pairingWindow?.Close();
                
                try
                {
                    // Detener servicios de forma asíncrona pero esperar un poco
                    _host.StopAsync(TimeSpan.FromSeconds(2)).Wait();
                }
                catch { }
                
                Log.Information("App termination complete");
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

        // Si cambia a integrado, ya no necesitamos el chequeo inicial
        if (state == ConnectionState.Paired)
        {
            _initialCheckPerformed = true;
        }
    }

    private void OnPairingCodeReceived(object? sender, string code)
    {
        Log.Information("Pairing code received in tray: {Code}", code);
        
        OpenMainWindow();
        _mainWindow?.ShowPairingPopup(code);
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        _initialCheckPerformed = true;
        if (_mainWindow == null || !_mainWindow.IsVisible)
        {
            if (_mainWindow == null)
            {
                _mainWindow = new ProductionMainWindow(
                    _cloverService,
                    _configService,
                    _queueService,
                    _inboxService,
                    _mpService
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
        OpenMainWindow();
        _mainWindow?.ShowPairingPopup();
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
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var appPath = !string.IsNullOrEmpty(exePath) 
                ? Path.GetDirectoryName(exePath) 
                : AppContext.BaseDirectory;
            
            var configPath = Path.Combine(appPath ?? "", "config");
            if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
            Process.Start("explorer.exe", configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening configuration folder");
        }
    }

    private void OpenLogs()
    {
        if (_mainWindow == null)
        {
            OpenMainWindow();
        }
        
        _mainWindow?.Dispatcher.Invoke(() => {
            var logWin = new LogWindow();
            logWin.Owner = _mainWindow;
            logWin.Show();
        });
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
