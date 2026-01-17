using CloverBridge.Models;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CloverBridge.Services;

/// <summary>
/// Cola de transacciones FIFO
/// </summary>
public class TransactionQueueService : BackgroundService
{
    private readonly ConfigurationService _configService;
    private readonly CloverWebSocketService _cloverService;
    private readonly ConcurrentQueue<PendingTransaction> _queue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();
    
    public TransactionQueueService(
        ConfigurationService configService,
        CloverWebSocketService cloverService)
    {
        _configService = configService;
        _cloverService = cloverService;

        // Suscribirse a mensajes de Clover
        _cloverService.MessageReceived += OnCloverMessageReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("TransactionQueueService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var transaction))
                {
                    await ProcessTransactionAsync(transaction, stoppingToken);
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing transaction");
            }
        }

        Log.Information("TransactionQueueService stopped");
    }

    public async Task<object> EnqueueTransactionAsync(CloverMessage message)
    {
        var tcs = new TaskCompletionSource<object>();
        var transaction = new PendingTransaction
        {
            Message = message,
            CompletionSource = tcs,
            EnqueuedAt = DateTime.UtcNow
        };

        _pendingRequests[message.Id!] = tcs;
        _queue.Enqueue(transaction);

        Log.Information("Transaction enqueued: {Id} {Method}", message.Id, message.Method);

        // Esperar respuesta con timeout
        var config = _configService.GetConfig();
        var timeoutTask = Task.Delay(config.Transaction.TimeoutMs);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _pendingRequests.TryRemove(message.Id!, out _);
            Log.Warning("Transaction timeout: {Id}", message.Id);
            return new { success = false, error = "Transaction timeout" };
        }

        return await tcs.Task;
    }

    private async Task ProcessTransactionAsync(PendingTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Processing transaction: {Id} {Method}", 
                transaction.Message.Id, 
                transaction.Message.Method);

            // Enviar mensaje a Clover
            await _cloverService.SendMessageAsync(transaction.Message);

            // La respuesta se manejará en OnCloverMessageReceived
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing transaction {Id}", transaction.Message.Id);
            
            if (_pendingRequests.TryRemove(transaction.Message.Id!, out var tcs))
            {
                tcs.SetResult(new { success = false, error = ex.Message });
            }
        }
    }

    private void OnCloverMessageReceived(object? sender, CloverMessage message)
    {
        try
        {
            // Buscar la transacción pendiente
            if (!string.IsNullOrEmpty(message.Id) && _pendingRequests.TryRemove(message.Id, out var tcs))
            {
                Log.Information("Transaction response received: {Id} {Method}", message.Id, message.Method);
                tcs.SetResult(message.Payload ?? new { success = true });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling Clover message");
        }
    }

    public object GetStatus()
    {
        return new
        {
            pending = _queue.Count,
            processing = _pendingRequests.Count
        };
    }

    private class PendingTransaction
    {
        public CloverMessage Message { get; set; } = null!;
        public TaskCompletionSource<object> CompletionSource { get; set; } = null!;
        public DateTime EnqueuedAt { get; set; }
    }
}
