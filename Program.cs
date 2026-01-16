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

            if (isService)
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
        // Usar carpeta del ejecutable en lugar de AppData
        var appPath = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        var logPath = Path.Combine(appPath, "logs", "clover-bridge-.log");
        
        Directory.CreateDirectory(Path.Combine(appPath, "logs"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
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

        Log.Information("Services started, creating ProductionMainWindow...");

        // Crear ventana principal
        var mainWindow = new ProductionMainWindow(cloverService, configService, queueService, inboxService);
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
