using System;
using System.Text.Json.Serialization;
using System.Reflection;

namespace CloverBridge.Models;

/// <summary>
/// Configuración principal de la aplicación
/// </summary>
public class AppConfig
{
    [JsonPropertyName("clover")]
    public CloverConfig Clover { get; set; } = new();

    [JsonPropertyName("api")]
    public ApiConfig Api { get; set; } = new();

    [JsonPropertyName("folders")]
    public FoldersConfig Folders { get; set; } = new();

    [JsonPropertyName("transaction")]
    public TransactionConfig Transaction { get; set; } = new();
}

public class CloverConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "10.1.1.53";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 12345;

    [JsonPropertyName("secure")]
    public bool Secure { get; set; } = false;

    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("remoteAppId")]
    public string RemoteAppId { get; set; } = "clover-bridge";

    [JsonPropertyName("posName")]
    public string PosName { get; set; } = "ERP Bridge";

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = "CB-001";

    [JsonPropertyName("reconnectDelayMs")]
    public int ReconnectDelayMs { get; set; } = 5000;

    [JsonPropertyName("maxReconnectAttempts")]
    public int MaxReconnectAttempts { get; set; } = 10;

    public string GetWebSocketUrl()
    {
        var protocol = Secure ? "wss" : "ws";
        return $"{protocol}://{Host}:{Port}/remote_pay";
    }
}

public class ApiConfig
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 3777;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";
}

public class FoldersConfig
{
    private static string GetExecutableDirectory()
    {
        return AppContext.BaseDirectory ?? System.Environment.CurrentDirectory;
    }

    [JsonPropertyName("inbox")]
    public string Inbox { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "INBOX");

    [JsonPropertyName("outbox")]
    public string Outbox { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "OUTBOX");

    [JsonPropertyName("archive")]
    public string Archive { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "ARCHIVE");
}

public class TransactionConfig
{
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 120000;

    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; } = 1;
}
