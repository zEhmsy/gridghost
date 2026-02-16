using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class AppConfig
{
    public List<DeviceInstance> Devices { get; set; } = new();
}

public class ConfigurationService
{
    private readonly string _configPath;
    private readonly ILogSink _logger;

    public ConfigurationService(ILogSink logger)
    {
        _logger = logger;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GridGhost");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "config.json");
    }

    public void Save(IEnumerable<DeviceInstance> devices)
    {
        try
        {
            var config = new AppConfig { Devices = devices.ToList() };
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
            _logger.Log("Info", $"Configuration saved to {_configPath}", "System");
        }
        catch (Exception ex)
        {
            _logger.Log("Error", $"Failed to save configuration: {ex.Message}", "System");
        }
    }

    public List<DeviceInstance> Load()
    {
        if (!File.Exists(_configPath)) return new List<DeviceInstance>();

        try
        {
            var json = File.ReadAllText(_configPath);
            var options = new JsonSerializerOptions 
            { 
                Converters = { new JsonStringEnumConverter() }
            };
            var config = JsonSerializer.Deserialize<AppConfig>(json, options);
            
            // Restore default state
            if (config?.Devices != null)
            {
                 foreach (var d in config.Devices)
                 {
                     // Ensure non-persisted state is reset
                     d.State = DeviceInstance.DeviceState.Stopped;
                     d.LastError = null;
                     // Enabled state is persisted but we force it to false or allow auto-start?
                     // Requirements say "Deterministic Start". Auto-start might be dangerous if port conflict.
                     // Better force false.
                     d.Enabled = false; 
                 }
                 return config.Devices;
            }
        }
        catch (Exception ex)
        {
            _logger.Log("Error", $"Failed to load configuration: {ex.Message}", "System");
        }
        return new List<DeviceInstance>();
    }
}
