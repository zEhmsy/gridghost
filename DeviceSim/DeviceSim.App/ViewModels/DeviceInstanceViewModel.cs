using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;

namespace DeviceSim.App.ViewModels;

public partial class DeviceInstanceViewModel : ViewModelBase
{
    private readonly DeviceInstance _instance;
    private readonly DeviceManager _deviceManager;

    public string Id => _instance.Id;
    public string Name => _instance.Name;
    public string Protocol => _instance.Protocol.ToString();
    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private bool _enabled;

    partial void OnEnabledChanged(bool value)
    {
        // Fire and forget async toggle
        // We use Dispatcher or Task.Run to avoid blocking the UI thread if it's synchronous
        // But the Manager methods are async.
        // We need to ensure we don't re-trigger if the value was set FROM the model update.
        
        if (value && _instance.Status != DeviceStatus.Running)
        {
            _ = _deviceManager.StartDeviceAsync(Id);
        }
        else if (!value && _instance.Status == DeviceStatus.Running)
        {
            _ = _deviceManager.StopDeviceAsync(Id);
        }
    }
    
    [ObservableProperty]
    private string? _lastError;

    public int Port
    {
        get => _instance.Network.Port;
        set
        {
            if (_instance.Network.Port != value)
            {
                _instance.Network.Port = value;
                OnPropertyChanged();
                
                // Restart if running to apply port change
                if (_instance.Status == DeviceStatus.Running)
                {
                    _ = Toggle(); // Stop
                    _ = Toggle(); // Start again with new port
                }
            }
        }
    }

    public DeviceInstanceViewModel(DeviceInstance instance, DeviceManager deviceManager)
    {
        _instance = instance;
        _deviceManager = deviceManager;
        _status = _instance.Status.ToString(); // Initialize backing field
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
         Status = _instance.Status.ToString();
         Enabled = _instance.Enabled;
         LastError = _instance.LastError;
         OnPropertyChanged(nameof(Status));
         OnPropertyChanged(nameof(Enabled));
         OnPropertyChanged(nameof(LastError));
         OnPropertyChanged(nameof(Port));
    }

    [RelayCommand]
    public async Task Toggle()
    {
        // Use the actual instance status from the model to decide action,
        // rather than the ViewModel property which might have been toggled by UI binding already.
        if (_instance.Status == DeviceStatus.Running)
        {
            await _deviceManager.StopDeviceAsync(_instance.Id);
        }
        else
        {
            await _deviceManager.StartDeviceAsync(_instance.Id);
        }
        
        UpdateFromModel();
    }
}
