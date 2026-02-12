using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;

namespace DeviceSim.App.ViewModels;

public partial class TemplatesViewModel : ViewModelBase
{
    private readonly TemplateRepository _repository;
    private readonly DeviceManager _deviceManager; // To create instances

    [ObservableProperty]
    private ObservableCollection<DeviceTemplate> _templates = new();

    [ObservableProperty]
    private DeviceTemplate? _selectedTemplate;

    public TemplatesViewModel(TemplateRepository repository, DeviceManager deviceManager)
    {
        _repository = repository;
        _deviceManager = deviceManager;
        LoadTemplates();
    }

    private async void LoadTemplates()
    {
        var list = await _repository.LoadAllAsync();
        Templates = new ObservableCollection<DeviceTemplate>(list);
    }

    [RelayCommand]
    public async Task CreateInstance()
    {
        if (SelectedTemplate == null) return;
        
        var instance = DeviceInstance.FromTemplate(SelectedTemplate);
        _deviceManager.AddInstance(instance);
        
        // Notify user? 
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task Delete()
    {
        if (SelectedTemplate == null) return;
        _repository.Delete(SelectedTemplate.Id);
        Templates.Remove(SelectedTemplate);
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task Import()
    {
        // Requires FilePicker, which is View concern.
        // We can use a service or interact with TopLevel.
        // For MVP, assume we place files in folder manually or use pre-loaded.
        await Task.CompletedTask;
    }
}
