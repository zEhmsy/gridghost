using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Avalonia.Threading;

namespace DeviceSim.App.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly DeviceManager _deviceManager;

    public ObservableCollection<DeviceInstanceViewModel> Devices { get; } = new();

    public DevicesViewModel(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        _deviceManager.OnDeviceUpdated += OnDeviceUpdated;
        
        LoadDevices();
    }

    private void LoadDevices()
    {
        Devices.Clear();
        foreach (var d in _deviceManager.GetAll())
        {
            Devices.Add(new DeviceInstanceViewModel(d, _deviceManager));
        }
    }

    private void OnDeviceUpdated(DeviceInstance instance)
    {
        Dispatcher.UIThread.Post(() => 
        {
            var vm = Devices.FirstOrDefault(d => d.Id == instance.Id);
            if (vm != null)
            {
                vm.UpdateFromModel();
            }
            else
            {
                Devices.Add(new DeviceInstanceViewModel(instance, _deviceManager));
                System.Diagnostics.Debug.WriteLine($"[DevicesViewModel] Added device. Total Devices in view: {Devices.Count}");
            }
        });
    }

    [RelayCommand]
    public async Task Remove(DeviceInstanceViewModel vm)
    {
        await _deviceManager.RemoveInstanceAsync(vm.Id);
        Devices.Remove(vm);
    }
}
