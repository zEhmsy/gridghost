using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DeviceSim.Core.Models;

namespace DeviceSim.App.Services;

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
        return await Task.Run(() => 
        {
            var templates = new List<DeviceTemplate>();
            if (!Directory.Exists(_templatesPath)) return templates;

            try 
            {
                var files = Directory.EnumerateFiles(_templatesPath, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<DeviceTemplate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (template != null)
                        {
                            // Ensure ID matches filename if possible, or leave as is.
                            // We might trust the file content ID.
                            templates.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading template {file}: {ex.Message}");
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error listing templates: {ex.Message}");
            }
            
            return templates;
        });
    }

    public async Task SaveAsync(DeviceTemplate template)
    {
        string filename = template.Id;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            filename = filename.Replace(c, '_');
        }
        
        string targetPath = Path.Combine(_templatesPath, $"{filename}.json");
        string tempPath = targetPath + ".tmp";

        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        string json = JsonSerializer.Serialize(template, options);

        await File.WriteAllTextAsync(tempPath, json);
        
        File.Move(tempPath, targetPath, true);
    }

    public void Delete(string templateId)
    {
        // Sanitize ID
         string filename = templateId;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            filename = filename.Replace(c, '_');
        }
        
        var filePath = Path.Combine(_templatesPath, $"{filename}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    
    public async Task<DeviceTemplate?> ImportAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("File not found.", sourcePath);
        
        var json = await File.ReadAllTextAsync(sourcePath);
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        var template = JsonSerializer.Deserialize<DeviceTemplate>(json, options);
        
        if (template == null)
        {
            throw new InvalidOperationException("Failed to decode template from JSON.");
        }
        return template;
    }
}
