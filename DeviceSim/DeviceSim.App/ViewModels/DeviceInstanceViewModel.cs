using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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

    [RelayCommand]
    public void OpenPoints()
    {
        WeakReferenceMessenger.Default.Send(new SelectDeviceMessage(Id));
        WeakReferenceMessenger.Default.Send(new NavigationMessage("Points"));
    }

    [RelayCommand]
    public void OpenPointMap()
    {
        WeakReferenceMessenger.Default.Send(new SelectDeviceMessage(Id));
        WeakReferenceMessenger.Default.Send(new NavigationMessage("PointMap"));
    }

    [RelayCommand]
    public async Task ExportMap()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Name,Key,Type,Access,ModbusType,Address,Scale,BitField,NiagaraType,Unit,Generator");

            foreach (var p in _instance.Points)
            {
                if (p.Modbus == null) continue;

                string modbusType = p.Modbus.Kind;
                string address = p.Modbus.Address.ToString();
                
                // Format Address like 40001 if holding, 30001 if input etc (conventional)
                // However, raw address is often more useful for strict mapping.
                // Let's stick to raw 0-based address for now, or maybe add both.
                
                string bitfield = "";
                if (p.Modbus.BitField != null)
                {
                    bitfield = $"Bit {p.Modbus.BitField.StartBit} (Len {p.Modbus.BitField.BitLength})";
                }

                sb.AppendLine($"{p.Name},{p.Key},{p.Type},{p.Access},{modbusType},{address},{p.Modbus.Scale},{bitfield},{p.NiagaraType},{p.Unit},{p.Generator?.Type ?? "None"}");
            }

            var fileName = $"{_instance.Name.Replace(" ", "_")}_Map.csv";
            var path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), fileName);
            
            await System.IO.File.WriteAllTextAsync(path, sb.ToString());

            // Ideally show a notification/toast here.
            // For now, maybe just log it?
            // System.Diagnostics.Debug.WriteLine($"Exported to {path}");
            
            // Temporary: open the folder
             System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
        }
    }
}
