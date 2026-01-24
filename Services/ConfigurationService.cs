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
        Log.Information("📂 Ruta de configuración YAML: {Path}", _cloverYamlPath);

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
        try
        {
            if (File.Exists(_cloverYamlPath))
            {
                var yaml = File.ReadAllText(_cloverYamlPath);
                Log.Debug("📄 Contenido YAML leído: {Yaml}", yaml);
                
                var config = _deserializer.Deserialize<AppConfig>(yaml);
                
                if (config != null)
                {
                    Log.Information("✅ Configuración cargada desde YAML: IP={IP}, Puerto={Puerto}, WSS={WSS}, Token={Token}", 
                        config.Clover.Host, config.Clover.Port, config.Clover.Secure, !string.IsNullOrEmpty(config.Clover.AuthToken));
                    
                    return config;
                }
            }

            Log.Information("⚠️ No se encontró clover.yml o está vacío, creando uno nuevo con valores por defecto.");
            var defaultConfig = new AppConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error crítico cargando clover.yml. Formato inválido?");
            return new AppConfig();
        }
    }

    public void SaveConfig(AppConfig? config = null)
    {
        try
        {
            var configToSave = config ?? _config;
            
            var tokenSnippet = !string.IsNullOrEmpty(configToSave.Clover.AuthToken) 
                ? $"***{configToSave.Clover.AuthToken.Substring(Math.Max(0, configToSave.Clover.AuthToken.Length - 4))}" 
                : "VACIO";
                
            Log.Information("✍️ Guardando clover.yml. Token a persistir: {Token}", tokenSnippet);

            var yaml = _serializer.Serialize(configToSave);
            File.WriteAllText(_cloverYamlPath, yaml);
            Log.Information("💾 Archivo config/clover.yml actualizado con éxito");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error guardando configuración en clover.yml");
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
