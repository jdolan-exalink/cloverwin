using CloverBridge.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using System.Text.Json;

namespace CloverBridge.Services;

/// <summary>
/// Gestiona la configuración de la aplicación
/// </summary>
public class ConfigurationService
{
    private readonly string _configPath;
    private AppConfig _config;

    public ConfigurationService()
    {
        // Usar la carpeta del ejecutable en lugar de AppData
        var appPath = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        
        Directory.CreateDirectory(appPath);
        _configPath = Path.Combine(appPath, "config.json");

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
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Log.Information("Configuration loaded from {Path}", _configPath);
                    return config;
                }
            }

            // Crear configuración por defecto
            Log.Information("Creating default configuration");
            var defaultConfig = new AppConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading configuration, using defaults");
            return new AppConfig();
        }
    }

    private void SaveConfig(AppConfig? config = null)
    {
        try
        {
            var configToSave = config ?? _config;
            var json = JsonSerializer.Serialize(configToSave, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configPath, json);
            Log.Information("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving configuration");
        }
    }

    private void EnsureFoldersExist()
    {
        try
        {
            Directory.CreateDirectory(_config.Folders.Inbox);
            Directory.CreateDirectory(_config.Folders.Outbox);
            Directory.CreateDirectory(_config.Folders.Archive);
            Log.Information("Folders created/verified");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating folders");
        }
    }
}
