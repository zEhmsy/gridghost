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

    private bool _isUpdatingFromModel;

    partial void OnEnabledChanged(bool value)
    {
        if (_isUpdatingFromModel) return;

        if (value)
        {
            _ = _deviceManager.StartDeviceAsync(Id);
        }
        else
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
                if (_instance.State == DeviceInstance.DeviceState.Running)
                {
                    _ = _deviceManager.StopDeviceAsync(_instance.Id).ContinueWith(async t => 
                    {
                        await _deviceManager.StartDeviceAsync(_instance.Id);
                    });
                }
            }
        }
    }

    public DeviceInstanceViewModel(DeviceInstance instance, DeviceManager deviceManager)
    {
        _instance = instance;
        _deviceManager = deviceManager;
        _status = _instance.State.ToString(); 
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
         _isUpdatingFromModel = true;
         Status = _instance.State.ToString();
         Enabled = _instance.Enabled;
         LastError = _instance.LastError;
         OnPropertyChanged(nameof(Status));
         OnPropertyChanged(nameof(Enabled));
         OnPropertyChanged(nameof(LastError));
         OnPropertyChanged(nameof(Port));
         _isUpdatingFromModel = false;
    }

    [RelayCommand]
    public async Task Toggle()
    {
        if (_instance.State == DeviceInstance.DeviceState.Running)
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
