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
    [YamlDotNet.Serialization.YamlMember(Alias = "CLOVER")]
    public CloverConfig Clover { get; set; } = new();

    [JsonPropertyName("api")]
    [YamlDotNet.Serialization.YamlMember(Alias = "API")]
    public ApiConfig Api { get; set; } = new();

    [JsonPropertyName("folders")]
    [YamlDotNet.Serialization.YamlMember(Alias = "FOLDERS")]
    public FoldersConfig Folders { get; set; } = new();

    [JsonPropertyName("transaction")]
    [YamlDotNet.Serialization.YamlMember(Alias = "TRANSACTION")]
    public TransactionConfig Transaction { get; set; } = new();

    [JsonPropertyName("paymentProvider")]
    [YamlDotNet.Serialization.YamlMember(Alias = "PAYMENT_PROVIDER")]
    public string PaymentProvider { get; set; } = "CLOVER";

    [JsonPropertyName("qrmp")]
    [YamlDotNet.Serialization.YamlIgnore]
    public QrmpConfig Qrmp { get; set; } = new();
}

public class QrmpConfig
{
    [JsonPropertyName("accessToken")]
    [YamlDotNet.Serialization.YamlMember(Alias = "MP_ACCESS_TOKEN")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("enabled")]
    [YamlDotNet.Serialization.YamlMember(Alias = "ENABLED")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("userId")]
    [YamlDotNet.Serialization.YamlMember(Alias = "MP_USER_ID")]
    public long UserId { get; set; }

    [JsonPropertyName("externalStoreId")]
    [YamlDotNet.Serialization.YamlMember(Alias = "STORE_EXTERNAL_ID")]
    public string ExternalStoreId { get; set; } = "";

    [JsonPropertyName("externalPosId")]
    [YamlDotNet.Serialization.YamlMember(Alias = "POS_EXTERNAL_ID")]
    public string ExternalPosId { get; set; } = "";

    [JsonPropertyName("webhookUrl")]
    [YamlDotNet.Serialization.YamlMember(Alias = "WEBHOOK_URL")]
    public string WebhookUrl { get; set; } = "";

    [JsonPropertyName("webhookSecret")]
    [YamlDotNet.Serialization.YamlMember(Alias = "WEBHOOK_SECRET")]
    public string WebhookSecret { get; set; } = "";

    [JsonPropertyName("currency")]
    [YamlDotNet.Serialization.YamlMember(Alias = "CURRENCY")]
    public string Currency { get; set; } = "ARS";

    [JsonPropertyName("orderTtl")]
    [YamlDotNet.Serialization.YamlMember(Alias = "ORDER_TTL_SECONDS")]
    public int OrderTtlSeconds { get; set; } = 300;
}

public class CloverConfig
{
    [JsonPropertyName("host")]
    [YamlDotNet.Serialization.YamlMember(Alias = "IP")]
    public string Host { get; set; } = "10.1.1.53";

    [JsonPropertyName("enabled")]
    [YamlDotNet.Serialization.YamlMember(Alias = "ENABLED")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("port")]
    [YamlDotNet.Serialization.YamlMember(Alias = "PUERTO")]
    public int Port { get; set; } = 12345;

    [JsonPropertyName("secure")]
    [YamlDotNet.Serialization.YamlMember(Alias = "WSS")]
    public bool Secure { get; set; } = false;

    [JsonPropertyName("authToken")]
    [YamlDotNet.Serialization.YamlMember(Alias = "TOKEN")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("remoteAppId")]
    [YamlDotNet.Serialization.YamlMember(Alias = "REMOTE_APP_ID")]
    public string RemoteAppId { get; set; } = "clover-bridge";

    [JsonPropertyName("posName")]
    [YamlDotNet.Serialization.YamlMember(Alias = "POS_NAME")]
    public string PosName { get; set; } = "ERP Bridge";

    [JsonPropertyName("serialNumber")]
    [YamlDotNet.Serialization.YamlMember(Alias = "SERIAL")]
    public string SerialNumber { get; set; } = "CB-001";

    [JsonPropertyName("reconnectDelayMs")]
    [YamlDotNet.Serialization.YamlMember(Alias = "RECONNECT_DELAY")]
    public int ReconnectDelayMs { get; set; } = 5000;

    [JsonPropertyName("maxReconnectAttempts")]
    [YamlDotNet.Serialization.YamlMember(Alias = "MAX_ATTEMPTS")]
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
    [YamlDotNet.Serialization.YamlMember(Alias = "API_PORT")]
    public int Port { get; set; } = 3777;

    [JsonPropertyName("host")]
    [YamlDotNet.Serialization.YamlMember(Alias = "API_HOST")]
    public string Host { get; set; } = "127.0.0.1";
}

public class FoldersConfig
{
    private static string GetExecutableDirectory()
    {
        return AppContext.BaseDirectory ?? System.Environment.CurrentDirectory;
    }

    [JsonPropertyName("inbox")]
    [YamlDotNet.Serialization.YamlMember(Alias = "INBOX_DIR")]
    public string Inbox { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "INBOX");

    [JsonPropertyName("outbox")]
    [YamlDotNet.Serialization.YamlMember(Alias = "OUTBOX_DIR")]
    public string Outbox { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "OUTBOX");

    [JsonPropertyName("archive")]
    [YamlDotNet.Serialization.YamlMember(Alias = "ARCHIVE_DIR")]
    public string Archive { get; set; } = System.IO.Path.Combine(GetExecutableDirectory(), "ARCHIVE");
}

public class TransactionConfig
{
    [JsonPropertyName("timeoutMs")]
    [YamlDotNet.Serialization.YamlMember(Alias = "TIMEOUT_MS")]
    public int TimeoutMs { get; set; } = 120000;

    [JsonPropertyName("concurrency")]
    [YamlDotNet.Serialization.YamlMember(Alias = "CONCURRENCY")]
    public int Concurrency { get; set; } = 1;
}
