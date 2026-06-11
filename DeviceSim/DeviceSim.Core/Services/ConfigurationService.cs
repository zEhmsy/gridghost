using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class UiConfig
{
    public bool SidebarCollapsed { get; set; } = false;
}

public class NiagaraExportConfig
{
    public string ExportHost { get; set; } = "127.0.0.1";
    public string ExportPath { get; set; } = GetDefaultExportPath();

    public static string GetDefaultExportPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "GridGhost",
            "niagara-manifest.json");
    }
}

public class AppConfig
{
    public List<DeviceInstance> Devices { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public NiagaraExportConfig NiagaraExport { get; set; } = new();
}

public class ConfigurationService
{
    private readonly string _configPath;
    private readonly ILogSink _logger;
    public AppConfig CurrentConfig { get; private set; } = new();

    public ConfigurationService(ILogSink logger)
    {
        _logger = logger;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GridGhost");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "config.json");
        Load(); // Initial load
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var json = JsonSerializer.Serialize(CurrentConfig, options);
            File.WriteAllText(_configPath, json);
            _logger.Log("Info", $"Configuration saved to {_configPath}", "System");
        }
        catch (Exception ex)
        {
            _logger.Log("Error", $"Failed to save configuration: {ex.Message}", "System");
        }
    }

    public void Save(IEnumerable<DeviceInstance> devices)
    {
        CurrentConfig.Devices = devices.ToList();
        Save();
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath)) 
        {
            CurrentConfig = new AppConfig();
            return CurrentConfig;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var options = new JsonSerializerOptions 
            { 
                Converters = { new JsonStringEnumConverter() }
            };
            var config = JsonSerializer.Deserialize<AppConfig>(json, options);
            
            if (config != null)
            {
                CurrentConfig = config;
                CurrentConfig.Ui ??= new UiConfig();
                CurrentConfig.NiagaraExport ??= new NiagaraExportConfig();
                if (string.IsNullOrWhiteSpace(CurrentConfig.NiagaraExport.ExportPath))
                {
                    CurrentConfig.NiagaraExport.ExportPath = NiagaraExportConfig.GetDefaultExportPath();
                }
                if (string.IsNullOrWhiteSpace(CurrentConfig.NiagaraExport.ExportHost))
                {
                    CurrentConfig.NiagaraExport.ExportHost = "127.0.0.1";
                }
                // Restore default state for devices
                foreach (var d in CurrentConfig.Devices)
                {
                    d.State = DeviceInstance.DeviceState.Stopped;
                    d.LastError = null;
                    d.Enabled = false; 
                }
                return CurrentConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.Log("Error", $"Failed to load configuration: {ex.Message}", "System");
        }
        
        CurrentConfig = new AppConfig();
        return CurrentConfig;
    }

    public NiagaraManifestExportResult ExportNiagaraManifest(string path, string exportHost)
    {
        CurrentConfig.NiagaraExport.ExportPath = path;
        CurrentConfig.NiagaraExport.ExportHost = exportHost;
        var result = NiagaraManifestExporter.Export(CurrentConfig.Devices, CurrentConfig.NiagaraExport);
        _logger.Log(
            "Info",
            $"Niagara manifest exported to {result.Path} ({result.DeviceCount} devices, {result.PointCount} points)",
            "System");
        Save();
        return result;
    }
}
