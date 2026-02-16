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
        // Atomic Save
        // 1. Write to .tmp
        // 2. Move to .json (overwrite)
        
        // Sanitize ID for filename
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
        
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
        File.Move(tempPath, targetPath);
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
        if (!File.Exists(sourcePath)) return null;
        
        try 
        {
             var json = await File.ReadAllTextAsync(sourcePath);
             var template = JsonSerializer.Deserialize<DeviceTemplate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
             
             if (template != null)
             {
                 // Check if ID exists, maybe generate new ID if needed?
                 // For now, we keep original ID but user can change it in editor before saving if they want.
                 // Actually, if we import, we should probably just return the object and let the UI decide when to save.
                 return template;
             }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Import failed: {ex.Message}");
        }
        return null;
    }
}
