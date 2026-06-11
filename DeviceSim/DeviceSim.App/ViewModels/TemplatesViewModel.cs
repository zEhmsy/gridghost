using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using DeviceSim.App.Services;
using TemplateRepository = DeviceSim.App.Services.TemplateRepository;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace DeviceSim.App.ViewModels;

public partial class TemplatesViewModel : ViewModelBase, IChangeTracker
{
    private readonly TemplateRepository _repository;
    private readonly DeviceManager _deviceManager; // To create instances

    [ObservableProperty]
    private ObservableCollection<DeviceTemplate> _templates = new();

    [ObservableProperty]
    private DeviceTemplate? _selectedTemplate;

    [ObservableProperty]
    private TemplateEditorViewModel? _editorViewModel;

    [ObservableProperty]
    private bool _isEditing;

    public TemplatesViewModel(TemplateRepository repository, DeviceManager deviceManager)
    {
        _repository = repository;
        _deviceManager = deviceManager;
        StartBlankConfiguration();
        LoadTemplates();
    }

    partial void OnSelectedTemplateChanged(DeviceTemplate? value)
    {
        if (value != null)
        {
            EditorViewModel = new TemplateEditorViewModel(CreateTemplateCopy(value));
        }
    }

    private async void LoadTemplates()
    {
        var list = await _repository.LoadAllAsync();
        Templates = new ObservableCollection<DeviceTemplate>(list);
    }

    [RelayCommand]
    public void CreateNew()
    {
        StartBlankConfiguration();
    }

    [RelayCommand]
    public async Task SaveEditor()
    {
        if (EditorViewModel == null) return;

        if (!EditorViewModel.Validate())
        {
             // Show error? Properties on EditorVM bind to UI errors.
             return;
        }

        var template = EditorViewModel.GetTemplate();
        
        try
        {
            // Save to disk
            await _repository.SaveAsync(template);

            // Update list if new
            if (!Templates.Any(t => t.Id == template.Id))
            {
                Templates.Add(template);
            }
            else
            {
                // Refresh list item?
                var index = Templates.IndexOf(Templates.First(t => t.Id == template.Id));
                if (index >= 0) Templates[index] = template; 
            }

            EditorViewModel.ValidationErrors = "Template saved successfully.";
            EditorViewModel.HasErrors = false;
            EditorViewModel.IsDirty = false;
        }
        catch (Exception ex)
        {
            EditorViewModel.ValidationErrors = $"Failed to save template: {ex.Message}";
            EditorViewModel.HasErrors = true;
        }
    }

    public bool IsDirty => EditorViewModel?.IsDirty ?? false;

    public async Task<bool> SaveChangesAsync()
    {
        await SaveEditor();
        return EditorViewModel == null || !EditorViewModel.HasErrors;
    }

    public void DiscardChanges()
    {
        if (SelectedTemplate != null)
        {
            EditorViewModel = new TemplateEditorViewModel(CreateTemplateCopy(SelectedTemplate));
        }
        else
        {
            StartBlankConfiguration();
        }
    }

    [RelayCommand]
    public async Task Duplicate()
    {
        if (SelectedTemplate == null) return;
        
        // Deep copy via JSON serialize/deserialize or manual
        // Simple manual copy for top level, deep for points
        var json = System.Text.Json.JsonSerializer.Serialize(SelectedTemplate);
        var copy = System.Text.Json.JsonSerializer.Deserialize<DeviceTemplate>(json);
        
        if (copy != null)
        {
            copy.Id = Guid.NewGuid().ToString();
            copy.Name = $"{SelectedTemplate.Name} (Copy)";
            
            await _repository.SaveAsync(copy);
            Templates.Add(copy);
            SelectedTemplate = copy;
        }
    }

    [RelayCommand]
    public async Task CreateInstance()
    {
        if (EditorViewModel == null) return;

        if (!EditorViewModel.Validate())
        {
            return;
        }

        var template = EditorViewModel.GetTemplate();
        
        int port = template.Network?.Port ?? 502;
        byte devAddr = template.Network?.DeviceAddress ?? 1;
        if (_deviceManager.GetAll().Any(d => d.Network.Port == port && d.Network.DeviceAddress == devAddr && d.State == DeviceInstance.DeviceState.Running))
        {
            var msg = $"Cannot create device on port {port} and Device Address {devAddr}. A running device is already using this combination.";
            WeakReferenceMessenger.Default.Send(new ShowErrorDialogMessage("Collision", msg));
            return;
        }

        var instance = DeviceInstance.FromTemplate(template);
        System.Diagnostics.Debug.WriteLine($"[TemplatesViewModel] Creating instance {instance.Id}");
        _deviceManager.AddInstance(instance);
        await _deviceManager.StartDeviceAsync(instance.Id);
        System.Diagnostics.Debug.WriteLine($"[TemplatesViewModel] Total templates after create: {Templates.Count}");
        EditorViewModel.IsDirty = false;
        
        // Auto-navigate to Points view to monitor newly created device
        WeakReferenceMessenger.Default.Send(new NavigationMessage("Points"));
        WeakReferenceMessenger.Default.Send(new SelectDeviceMessage(instance.Id));
    }

    [RelayCommand]
    public async Task Delete()
    {
        if (SelectedTemplate == null) return;
        
        // Confirm?
        
        _repository.Delete(SelectedTemplate.Id);
        Templates.Remove(SelectedTemplate);
        SelectedTemplate = null;
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task Import()
    {
         // File picker logic should ideally be in View code-behind or a service
         // But for now we might leave this empty or mock it as requested "MVP assume place files manually"
         // or if we have access to TopLevel.
         
         // If we want to support it properly:
         if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
         {
             var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
             if (topLevel == null) return;

             var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
             {
                 Title = "Import Template",
                 AllowMultiple = false,
                 FileTypeFilter = new[] { new FilePickerFileType("JSON Templates") { Patterns = new[] { "*.json" } } }
             });

             if (files.Count > 0)
             {
                 var file = files[0];
                 var path = file.Path.LocalPath;
                 try
                 {
                     var template = await _repository.ImportAsync(path);
                     if (template != null)
                     {
                         // Ensure unique ID?
                         template.Id = Guid.NewGuid().ToString();
                         template.Name += " (Imported)";
                         await _repository.SaveAsync(template);
                         Templates.Add(template);
                         SelectedTemplate = template;
                         Console.WriteLine($"[TemplatesViewModel] Imported new template {template.Id}, count: {Templates.Count}");
                     }
                 }
                 catch (Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"[TemplatesViewModel] Import exception caught: {ex.Message}");
                     WeakReferenceMessenger.Default.Send(new ShowErrorDialogMessage("Import Failed", $"Failed to import template. The file may be corrupt or incorrectly formatted.\n\nError: {ex.Message}"));
                 }
             }
         }
    }

    private void StartBlankConfiguration()
    {
        SelectedTemplate = null;
        EditorViewModel = new TemplateEditorViewModel(new DeviceTemplate
        {
            Name = "Nuovo Dispositivo Custom",
            Protocol = ProtocolType.Modbus,
            Network = new NetworkConfig
            {
                Port = 502,
                DeviceAddress = 1
            }
        });
    }

    private static DeviceTemplate CreateTemplateCopy(DeviceTemplate template)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(template);
        var copy = System.Text.Json.JsonSerializer.Deserialize<DeviceTemplate>(json) ?? new DeviceTemplate();
        copy.Id = Guid.NewGuid().ToString();
        copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? "Nuovo Dispositivo Custom" : $"{copy.Name} Custom";
        return copy;
    }
}
