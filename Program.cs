using CloverBridge.Services;
using CloverBridge.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CloverBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Configurar Serilog
        ConfigureLogging();

        try
        {
            Log.Information("CloverBridge starting...");

            // Detectar modo de ejecución
            var isService = args.Contains("--service");
            var isConsole = args.Contains("--console");
            var isUIMode = args.Contains("--ui");
            var isTestOutbox = args.Contains("--test-outbox");

            if (isTestOutbox)
            {
                // Modo prueba OUTBOX
                Log.Information("Starting OUTBOX test mode");
                TestOutboxReader.TestOutboxAsync().GetAwaiter().GetResult();
                return;
            }
            else if (isService)
            {
                // Modo Windows Service
                Log.Information("Starting as Windows Service");
                RunAsServiceAsync().GetAwaiter().GetResult();
            }
            else if (isConsole)
            {
                // Modo consola (para debugging)
                Log.Information("Starting in console mode");
                RunAsConsoleAsync().GetAwaiter().GetResult();
            }
            else if (isUIMode)
            {
                // Modo UI completa (Testing Dashboard)
                Log.Information("Starting in UI mode");
                RunAsUIApp();
            }
            else
            {
                // Modo UI con System Tray (default)
                Log.Information("Starting in tray mode");
                RunAsTrayApp();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging()
    {
        // Usar la carpeta del ejecutable REAL
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var appPath = !string.IsNullOrEmpty(exePath) 
            ? Path.GetDirectoryName(exePath) 
            : AppContext.BaseDirectory;

        if (string.IsNullOrEmpty(appPath)) appPath = Environment.CurrentDirectory;
        
        var logsPath = Path.Combine(appPath, "logs");
        
        Directory.CreateDirectory(logsPath);

        // Log general de la aplicación
        var generalLogPath = Path.Combine(logsPath, "clover-bridge-.log");
        
        // Log específico de Clover
        var cloverLogPath = Path.Combine(logsPath, "clover-.log");

        // Log específico de Mercado Pago
        var mpLogPath = Path.Combine(logsPath, "mp-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            // Log general (Information y superior)
            .WriteTo.File(
                generalLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            // Log específico de Clover (filtrado por namespace o contenido)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(evt => 
                    evt.Properties.ContainsKey("SourceContext") && evt.Properties["SourceContext"].ToString().Contains("Clover") ||
                    evt.RenderMessage().Contains("Clover", StringComparison.OrdinalIgnoreCase))
                .WriteTo.File(
                    cloverLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
            )
            // Log específico de Mercado Pago (filtrado por namespace o contenido)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(evt => 
                    evt.Properties.ContainsKey("SourceContext") && evt.Properties["SourceContext"].ToString().Contains("MercadoPago") ||
                    evt.RenderMessage().Contains("MP:", StringComparison.OrdinalIgnoreCase) ||
                    evt.RenderMessage().Contains("Mercado Pago", StringComparison.OrdinalIgnoreCase))
                .WriteTo.File(
                    mpLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
            )
            .WriteTo.Sink(new CloverBridge.Services.LogWindowSink())
            .CreateLogger();
        
        Log.Information("═══════════════════════════════════════════════════════════════════");
        Log.Information("  CloverBridge - Log iniciado");
        Log.Information("  Hora: {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log.Information("  Carpeta de logs: {Path}", logsPath);
        Log.Information("═══════════════════════════════════════════════════════════════════");
    }

    private static async Task RunAsServiceAsync()
    {
        var builder = Host.CreateDefaultBuilder();
        
        builder.UseWindowsService(options =>
        {
            options.ServiceName = "CloverBridge";
        });

        builder.UseSerilog();

        builder.ConfigureServices(services =>
        {
            // Registrar servicios
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<CloverWebSocketService>();
            services.AddSingleton<TransactionQueueService>();
            services.AddSingleton<InboxWatcherService>();
            services.AddSingleton<ApiService>();
            services.AddSingleton<MercadoPagoService>();

            // Registrar como hosted services
            services.AddHostedService<CloverWebSocketService>(sp => sp.GetRequiredService<CloverWebSocketService>());
            services.AddHostedService<TransactionQueueService>(sp => sp.GetRequiredService<TransactionQueueService>());
            services.AddHostedService<InboxWatcherService>(sp => sp.GetRequiredService<InboxWatcherService>());
            services.AddHostedService<ApiService>(sp => sp.GetRequiredService<ApiService>());
        });

        var host = builder.Build();
        await host.RunAsync();
    }

    private static async Task RunAsConsoleAsync()
    {
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

        var host = builder.Build();
        
        Console.WriteLine("CloverBridge running in console mode. Press Ctrl+C to exit.");
        await host.RunAsync();
    }

    private static void RunAsTrayApp()
    {
        // Verificar si ya hay una instancia corriendo
        const string mutexName = "CloverBridge_SingleInstance_Mutex";
        bool isNewInstance = false;
        
        using var mutex = new Mutex(true, mutexName, out isNewInstance);
        
        if (!isNewInstance)
        {
            Log.Warning("CloverBridge is already running");
            MessageBox.Show(
                "CloverBridge ya se está ejecutando.\n\nNo se puede iniciar otra instancia.",
                "CloverBridge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            using var trayContext = new TrayApplicationContext();
            Application.Run(trayContext);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void RunAsUIApp()
    {
        Log.Information("Creating WPF application...");
        
        // Crear aplicación WPF primero (debe ser en el thread STA principal)
        var app = new System.Windows.Application();
        app.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        
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

        var host = builder.Build();

        Log.Information("Starting background services...");
        // Iniciar servicios en un Task separado
        _ = host.StartAsync();
        
        // Esperar un poco para que los servicios se inicien
        Thread.Sleep(1500);

        // Obtener servicios
        var cloverService = host.Services.GetRequiredService<CloverWebSocketService>();
        var configService = host.Services.GetRequiredService<ConfigurationService>();
        var queueService = host.Services.GetRequiredService<TransactionQueueService>();
        var inboxService = host.Services.GetRequiredService<InboxWatcherService>();
        var mpService = host.Services.GetRequiredService<MercadoPagoService>();

        Log.Information("Services started, creating ProductionMainWindow...");

        // Crear ventana principal
        var mainWindow = new ProductionMainWindow(cloverService, configService, queueService, inboxService, mpService);
        app.MainWindow = mainWindow;
        
        // Manejar cierre de aplicación para detener servicios
        app.Exit += async (s, e) =>
        {
            Log.Information("Application exiting, stopping services...");
            await host.StopAsync();
            host.Dispose();
        };
        
        Log.Information("Showing window and running application...");
        mainWindow.Show();
        app.Run();
        
        Log.Information("Application closed");
    }
}
