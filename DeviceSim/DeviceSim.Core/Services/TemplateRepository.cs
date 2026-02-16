using System.Text.Json;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class TemplateRepository
{
    private readonly string _templatesPath;

    public TemplateRepository(string templatesPath)
    {
        _templatesPath = templatesPath;
        if (!Directory.Exists(_templatesPath))
        {
            Directory.CreateDirectory(_templatesPath);
        }
    }

    public async Task<List<DeviceTemplate>> LoadAllAsync()
    {
        return await Task.Run(() => LoadAll());
    }

    public List<DeviceTemplate> LoadAll()
    {
        var templates = new List<DeviceTemplate>();
        
        if (!Directory.Exists(_templatesPath))
        {
             return templates;
        }

        var files = Directory.GetFiles(_templatesPath, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonSerializer.Deserialize<DeviceTemplate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (template != null)
                {
                    templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading template {file}: {ex.Message}");
            }
        }

        return templates;
    }

    public async Task SaveAsync(DeviceTemplate template)
    {
        var filePath = Path.Combine(_templatesPath, $"{template.Id}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(template, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public void Delete(string templateId)
    {
        var filePath = Path.Combine(_templatesPath, $"{templateId}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
