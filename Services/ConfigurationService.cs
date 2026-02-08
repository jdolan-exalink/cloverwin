using CloverBridge.Models;
using Serilog;
using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CloverBridge.Services;

/// <summary>
/// Gestiona la configuración de la aplicación usando YAML
/// </summary>
public class ConfigurationService
{
    private readonly string _configDir;
    private readonly string _cloverYamlPath;
    private readonly string _qrmpYamlPath;
    private AppConfig _config;
    
    // Serializadores de YAML
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public ConfigurationService()
    {
        // Ubicación base del ejecutable
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var appPath = !string.IsNullOrEmpty(exePath) 
            ? Path.GetDirectoryName(exePath) 
            : AppContext.BaseDirectory;
        
        if (string.IsNullOrEmpty(appPath)) appPath = Environment.CurrentDirectory;
        
        // Carpeta 'config'
        _configDir = Path.Combine(appPath, "config");
        Directory.CreateDirectory(_configDir);
        
        _cloverYamlPath = Path.Combine(_configDir, "clover.yml");
        _qrmpYamlPath = Path.Combine(_configDir, "qrmp.yml");
        
        Log.Information("📂 Rutas de configuración YAML: {CloverPath}, {QrmpPath}", _cloverYamlPath, _qrmpYamlPath);

        // Configurar YamlDotNet
        _serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance) // Usamos Alias en el modelo
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _config = LoadConfig();
        EnsureFoldersExist();
    }

    public AppConfig GetConfig() => _config;

    public void UpdateConfig(AppConfig newConfig)
    {
        _config = newConfig;
        SaveConfig();
    }

    public void UpdateCloverConfig(CloverConfig cloverConfig)
    {
        _config.Clover = cloverConfig;
        SaveConfig();
    }

    private AppConfig LoadConfig()
    {
        AppConfig config = new AppConfig();
        
        // 0. Intentar cargar appsettings.json si existe
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(appSettingsPath, System.Text.Encoding.UTF8);
                var appSettings = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);
                if (appSettings != null)
                {
                    config = appSettings;
                    Log.Information("📋 Configuración de appsettings.json cargada");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Advertencia cargando appsettings.json (continuando con defaults)");
            }
        }
        
        // 1. Cargar Clover config (puede sobrescribir appsettings)
        try
        {
            if (File.Exists(_cloverYamlPath))
            {
                var yaml = File.ReadAllText(_cloverYamlPath);
                var loaded = _deserializer.Deserialize<AppConfig>(yaml);
                if (loaded != null) 
                {
                    // Mantener configuración de carpetas de appsettings si están definidas
                    if (config.Folders?.UseCustomPaths == true || !string.IsNullOrEmpty(config.Folders?.DefaultBasePath))
                    {
                        // Preservar configuración de carpetas personalizadas
                        var customFolders = config.Folders;
                        config = loaded;
                        config.Folders = customFolders;
                    }
                    else
                    {
                        config = loaded;
                    }
                }
                Log.Information("✅ Configuración Clover cargada");
            }
            else
            {
                Log.Warning("⚠️ clover.yml no encontrado, se creará uno nuevo.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error cargando clover.yml");
        }

        // 2. Cargar MP QR config
        try
        {
            if (File.Exists(_qrmpYamlPath))
            {
                var yaml = File.ReadAllText(_qrmpYamlPath);
                var loaded = _deserializer.Deserialize<QrmpConfig>(yaml);
                if (loaded != null) config.Qrmp = loaded;
                Log.Information("✅ Configuración Mercado Pago cargada");
            }
            else
            {
                Log.Warning("⚠️ qrmp.yml no encontrado, se creará uno nuevo.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error cargando qrmp.yml");
        }
        
        // 3. Registrar las rutas cargadas
        Log.Information("📁 Carpetas configuradas:");
        Log.Information("   📥 INBOX: {Inbox}", config.Folders.Inbox);
        Log.Information("   📤 OUTBOX: {Outbox}", config.Folders.Outbox);
        Log.Information("   📦 ARCHIVE: {Archive}", config.Folders.Archive);

        return config;
    }

    public void SaveConfig(AppConfig? config = null)
    {
        try
        {
            var configToSave = config ?? _config;
            
            // Guardar clover.yml
            var cloverYaml = _serializer.Serialize(configToSave);
            File.WriteAllText(_cloverYamlPath, cloverYaml);
            
            // Guardar qrmp.yml
            var qrmpYaml = _serializer.Serialize(configToSave.Qrmp);
            File.WriteAllText(_qrmpYamlPath, qrmpYaml);

            Log.Information("💾 Archivos de configuración actualizados");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error guardando configuración");
        }
    }

    private void EnsureFoldersExist()
    {
        try
        {
            Directory.CreateDirectory(_config.Folders.Inbox);
            Directory.CreateDirectory(_config.Folders.Outbox);
            Directory.CreateDirectory(_config.Folders.Archive);
            Log.Information("📁 Carpetas de trabajo verificadas");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error creando carpetas de trabajo");
        }
    }
}
