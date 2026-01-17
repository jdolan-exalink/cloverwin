using CloverBridge.Models;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CloverBridge.Services;

/// <summary>
/// Monitorea carpeta INBOX para solicitudes del ERP
/// </summary>
public class InboxWatcherService : BackgroundService
{
    private readonly ConfigurationService _configService;
    private readonly CloverWebSocketService _cloverService;
    private readonly TransactionQueueService _queueService;
    private FileSystemWatcher? _watcher;

    public InboxWatcherService(
        ConfigurationService configService,
        CloverWebSocketService cloverService,
        TransactionQueueService queueService)
    {
        _configService = configService;
        _cloverService = cloverService;
        _queueService = queueService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configService.GetConfig();
        var inboxPath = config.Folders.Inbox;

        Directory.CreateDirectory(inboxPath);

        _watcher = new FileSystemWatcher(inboxPath)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;

        Log.Information("InboxWatcher started, monitoring: {Path}", inboxPath);

        // Procesar archivos existentes
        ProcessExistingFiles(inboxPath);

        return Task.CompletedTask;
    }

    private void ProcessExistingFiles(string inboxPath)
    {
        try
        {
            var files = Directory.GetFiles(inboxPath, "*.json");
            Log.Information("Found {Count} existing files in inbox", files.Length);

            foreach (var file in files)
            {
                _ = ProcessFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing existing files");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        Log.Information("New file detected: {Path}", e.FullPath);
        _ = ProcessFileAsync(e.FullPath);
    }

    private async Task ProcessFileAsync(string filePath)
    {
        try
        {
            // Esperar a que el archivo esté completamente escrito
            await Task.Delay(500);

            Log.Information("Processing file: {Path}", filePath);

            // Leer archivo
            var json = await File.ReadAllTextAsync(filePath);
            var message = JsonSerializer.Deserialize<CloverMessage>(json);

            if (message == null)
            {
                Log.Warning("Invalid message in file: {Path}", filePath);
                await MoveToArchiveAsync(filePath, success: false);
                return;
            }

            // Asegurar que tiene ID
            if (string.IsNullOrEmpty(message.Id))
            {
                message.Id = Guid.NewGuid().ToString();
            }

            // Procesar según el método
            object response = message.Method switch
            {
                "TX_START" or "SALE" => await _queueService.EnqueueTransactionAsync(message),
                "VOID_PAYMENT" => await _queueService.EnqueueTransactionAsync(message),
                "REFUND_PAYMENT" => await _queueService.EnqueueTransactionAsync(message),
                "SHOW_DISPLAY_ORDER" => await _queueService.EnqueueTransactionAsync(message),
                _ => new { success = false, error = "Unknown method" }
            };

            // Escribir respuesta en OUTBOX
            await WriteResponseAsync(message.Id, response);

            // Archivar archivo original
            await MoveToArchiveAsync(filePath, success: true);

            Log.Information("File processed successfully: {Path}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing file: {Path}", filePath);
            await MoveToArchiveAsync(filePath, success: false);
        }
    }

    private async Task WriteResponseAsync(string requestId, object response)
    {
        try
        {
            var config = _configService.GetConfig();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"RES_{timestamp}_{requestId}.json";
            var tempPath = Path.Combine(config.Folders.Outbox, filename + ".tmp");
            var finalPath = Path.Combine(config.Folders.Outbox, filename);

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Escritura atómica: .tmp -> .json
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, finalPath, overwrite: true);

            Log.Information("Response written: {Path}", finalPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing response for request {Id}", requestId);
        }
    }

    private async Task MoveToArchiveAsync(string filePath, bool success)
    {
        try
        {
            var config = _configService.GetConfig();
            var subfolder = success ? "processed" : "failed";
            var archivePath = Path.Combine(config.Folders.Archive, subfolder);
            
            Directory.CreateDirectory(archivePath);

            var fileName = Path.GetFileName(filePath);
            var destination = Path.Combine(archivePath, fileName);

            // Si el archivo ya existe, agregar timestamp
            if (File.Exists(destination))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                fileName = $"{nameWithoutExt}_{timestamp}{ext}";
                destination = Path.Combine(archivePath, fileName);
            }

            File.Move(filePath, destination);
            Log.Information("File archived: {Path}", destination);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error archiving file: {Path}", filePath);
            
            // Intentar eliminar el archivo original
            try
            {
                File.Delete(filePath);
            }
            catch { }
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
