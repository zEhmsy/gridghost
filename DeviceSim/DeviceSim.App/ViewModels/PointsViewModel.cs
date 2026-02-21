using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Avalonia.Threading;

namespace DeviceSim.App.ViewModels;

public partial class PointsViewModel : ViewModelBase, IChangeTracker
{
    private readonly IPointStore _pointStore;
    private readonly DeviceManager _deviceManager;

    public ObservableCollection<PointViewModel> Points { get; } = new();

    public ObservableCollection<DevicePointsGroupViewModel> DeviceGroups { get; } = new();

    [ObservableProperty]
    private string? _selectedDeviceId;

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _isDirty;

    // Per-device dirty tracking — only config-field edits set this
    private readonly System.Collections.Generic.HashSet<string> _dirtyDeviceIds = new();

    // Properties that originate from simulation/store updates — must NOT set dirty
    private static readonly System.Collections.Generic.HashSet<string> _nonDirtyProps = new()
    {
        nameof(PointViewModel.Value),
        nameof(PointViewModel.StringValue),
        nameof(PointViewModel.BoolValue),
        nameof(PointViewModel.DisplayValue),
        nameof(PointViewModel.EffectiveDisplayValue),
        nameof(PointViewModel.LastUpdated),
        nameof(PointViewModel.Source),
        nameof(PointViewModel.OverrideStatus),
        nameof(PointViewModel.IsEditingAllowed), // set by device start/stop
        nameof(PointViewModel.IsSettingsOpen),   // UI-only toggle
    };

    public PointsViewModel(IPointStore pointStore, DeviceManager deviceManager)
    {
        _pointStore = pointStore;
        _deviceManager = deviceManager;
        _pointStore.OnPointChanged += OnPointChanged;
        _deviceManager.OnDeviceUpdated += OnDeviceUpdated;
        _deviceManager.OnDeviceRemoved += OnDeviceRemoved;
        
        WeakReferenceMessenger.Default.Register<SelectDeviceMessage>(this, (r, m) =>
        {
            SelectedDeviceId = m.DeviceId;
            if (m.DeviceId != null)
            {
                foreach (var g in DeviceGroups)
                {
                    g.IsExpanded = (g.DeviceId == m.DeviceId);
                }
            }
        });

        RefreshPoints();
    }

    private void RefreshPoints()
    {
        Points.Clear();
        DeviceGroups.Clear();
        var devices = _deviceManager.GetAll().ToList();
        foreach (var device in devices)
        {
            var group = new DevicePointsGroupViewModel(device, _deviceManager);
            if (SelectedDeviceId == device.Id) group.IsExpanded = true;
            DeviceGroups.Add(group);

            foreach (var pointDef in device.Points)
            {
                if (_pointStore.TryGetValue(device.Id, pointDef.Key, out var val))
                {
                    var vm = new PointViewModel(device, pointDef, val, _pointStore, _deviceManager);
                    vm.PropertyChanged += PointViewModel_PropertyChanged;
                    Points.Add(vm);
                    group.Points.Add(vm);
                }
            }
        }
        _dirtyDeviceIds.Clear();
        IsDirty = false;
    }

    private void PointViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null || _nonDirtyProps.Contains(e.PropertyName)) return;

        if (sender is PointViewModel pvm)
        {
            _dirtyDeviceIds.Add(pvm.DeviceId);
        }
        IsDirty = _dirtyDeviceIds.Count > 0;
    }

    public bool IsDeviceDirty(string deviceId) => _dirtyDeviceIds.Contains(deviceId);

    private void OnDeviceRemoved(string deviceId)
    {
        Dispatcher.UIThread.Post(() => {
            var g = DeviceGroups.FirstOrDefault(x => x.DeviceId == deviceId);
            if (g != null) DeviceGroups.Remove(g);

            var toRemove = Points.Where(p => p.DeviceId == deviceId).ToList();
            foreach (var p in toRemove) Points.Remove(p);
        });
    }

    private void OnDeviceUpdated(DeviceInstance instance)
    {
        Dispatcher.UIThread.Post(() => {
            var group = DeviceGroups.FirstOrDefault(x => x.DeviceId == instance.Id);
            if (group != null) group.UpdateFromModel();

            // Check if device points are already loaded
            var existingPoints = Points.Where(pt => pt.DeviceId == instance.Id).ToList();
            if (existingPoints.Count == 0 && instance.Points.Count > 0)
            {
                if (group == null)
                {
                    group = new DevicePointsGroupViewModel(instance, _deviceManager);
                    DeviceGroups.Add(group);
                }

                System.Diagnostics.Debug.WriteLine($"[PointsViewModel] Adding {instance.Points.Count} points for NEW device {instance.Id}");
                foreach (var pointDef in instance.Points)
                {
                    if (_pointStore.TryGetValue(instance.Id, pointDef.Key, out var val))
                    {
                        var vm = new PointViewModel(instance, pointDef, val, _pointStore, _deviceManager);
                        vm.PropertyChanged += PointViewModel_PropertyChanged;
                        Points.Add(vm);
                        group.Points.Add(vm);
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PointsViewModel] Updating editing state for {existingPoints.Count} points of device {instance.Id}");
                bool isAllowed = instance.State != DeviceInstance.DeviceState.Running;
                foreach (var p in existingPoints)
                {
                    p.IsEditingAllowed = isAllowed;
                }
            }
        });
    }

    private void OnPointChanged(string deviceId, string key, PointValue val)
    {
        Dispatcher.UIThread.Post(() => 
        {
            var vm = Points.FirstOrDefault(p => p.DeviceId == deviceId && p.Key == key);
            if (vm != null)
            {
                vm.Update(val);
            }
        });
    }

    [RelayCommand]
    public void Refresh() => RefreshPoints();

    [RelayCommand]
    public async Task Save()
    {
        await SaveChangesAsync();
    }

    public async Task<bool> SaveChangesAsync()
    {
        // Force commit of all SetValue properties (since SetValue is usually called manually, we ensure generator properties are set on the models)
        foreach (var p in Points)
        {
            await p.CommitConfigAsync();
        }
        _deviceManager.SaveAll();
        _dirtyDeviceIds.Clear();
        IsDirty = false;
        return true;
    }

    public void DiscardChanges()
    {
        _dirtyDeviceIds.Clear();
        IsDirty = false;
        RefreshPoints();
    }
}

public partial class PointViewModel : ObservableObject
{
    private readonly IPointStore _store;
    private readonly DeviceManager _deviceManager;
    private readonly DeviceInstance _device;
    private readonly PointDefinition _def;

    public string DeviceId => _device.Id;
    public string DeviceName => _device.Name;
    public string Key => _def.Key;

    public string[] AvailableTypes { get; } = new[] { "bool", "int16", "uint16", "int32", "uint32", "float" };
    public string[] AvailableNiagaraTypes { get; } = new[] { "Boolean", "Numeric", "Enum" };
    public string[] AvailableGenTypes { get; } = new[] { "static", "sine", "random", "ramp" };

    [ObservableProperty] private string _type;
    [ObservableProperty] private string _niagaraType;
    
    private object _value = 0;
    public object Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(EffectiveDisplayValue));
                OnPropertyChanged(nameof(StringValue));
                OnPropertyChanged(nameof(BoolValue));

                if (IsStatic)
                {
                    object val = value;
                    if (value is string str)
                    {
                        if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) val = d;
                        else if (bool.TryParse(str, out bool b)) val = b;
                    }
                    string disp = val.ToString() ?? "";
                    if (val is double dv) disp = dv.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    
                    _store.SetValue(DeviceId, Key, val, PointSource.Manual, disp);
                }
            }
        }
    }

    public string StringValue
    {
        get => Value?.ToString() ?? "0";
        set 
        {
            if (_def.Type == "bool") return;
            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
            {
                Value = d;
            }
        }
    }

    public bool BoolValue
    {
        get => Value is bool b ? b : false;
        set 
        {
            if (_def.Type != "bool") return;
            Value = value;
        }
    }
    [ObservableProperty] private string? _displayValue;
    [ObservableProperty] private string? _overrideStatus;
    [ObservableProperty] private string _source; 
    [ObservableProperty] private DateTime _lastUpdated;
    [ObservableProperty] private ushort _address;
    [ObservableProperty] private double _scale;
    [ObservableProperty] private int _startBit;
    [ObservableProperty] private int _bitLength;

    public string ModbusAddress => _def.Modbus?.Kind.ToLower() switch
    {
        "coil" => $"0{(_def.Modbus.Address + 1):D4}",
        "discrete" => $"1{(_def.Modbus.Address + 1):D4}",
        "input" => $"3{(_def.Modbus.Address + 1):D4}",
        "holding" => $"4{(_def.Modbus.Address + 1):D4}",
        _ => "N/A"
    };

    public string FormattedNiagaraType => Type == "bool" ? "Boolean" : NiagaraType;

    public string BitLengthString 
    {
        get 
        {
            if (_def.Modbus?.BitField != null)
                return $"bit={_def.Modbus.BitField.StartBit}"; // Simple display for single bit
            // If enum bitfield, maybe "start-end"? 
            if (_def.Modbus?.Kind == "Holding" || _def.Modbus?.Kind == "Input")
            {
                 // Check if it's a bitfield config
                 if (_def.Modbus.BitField != null)
                    return $"{_def.Modbus.BitField.StartBit}:{_def.Modbus.BitField.BitLength}";
            }
            return "";
        }
    }

    [ObservableProperty] private string _genType;
    [ObservableProperty] private double _genMin;
    [ObservableProperty] private double _genMax;
    [ObservableProperty] private double _genPeriod;
    [ObservableProperty] private double _genStep;
    [ObservableProperty] private bool _isEditingAllowed;
    [ObservableProperty] private bool _isStatic;
    [ObservableProperty] private bool _isSettingsOpen;

    // Derived property for UI to avoid blanking when Store sends empty value
    public string EffectiveDisplayValue 
    {
        get
        {
            if (Value == null) return "-";
            
            if (_def.Type == "bool") 
            {
                if (Value is bool b) return b ? "True" : "False";
                return "False"; // Deterministic fallback
            }
            
            // Numeric formatting
            if (Value is double d) return d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            if (Value is float f) return f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            if (Value is int i) return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            
            return Value.ToString() ?? "";
        }
    }



    partial void OnGenTypeChanged(string value)
    {
        IsStatic = value == "static";
        if (IsStatic) IsSettingsOpen = false;
    }

    public PointViewModel(DeviceInstance device, PointDefinition def, PointValue initial, IPointStore store, DeviceManager deviceManager)
    {
        _device = device;
        _def = def;
        _store = store;
        _deviceManager = deviceManager;
        
        _type = def.Type;
        _niagaraType = def.NiagaraType;
        _value = initial.Value;
        _displayValue = initial.DisplayValue;
        _source = initial.Source.ToString();
        _lastUpdated = initial.LastUpdated;

        if (def.Modbus != null)
        {
            _address = def.Modbus.Address;
            _scale = def.Modbus.Scale;
            if (def.Modbus.BitField != null)
            {
                _startBit = def.Modbus.BitField.StartBit;
                _bitLength = def.Modbus.BitField.BitLength;
            }
        }

        if (def.Generator == null) def.Generator = new GeneratorConfig();
        _genType = def.Generator.Type;
        _genMin = def.Generator.Min;
        _genMax = def.Generator.Max;
        _genPeriod = def.Generator.PeriodSeconds;
        _genStep = def.Generator.Step;
        
        _isStatic = _genType == "static";
        _isEditingAllowed = _device.State != DeviceInstance.DeviceState.Running;
    }

    public void Update(PointValue val)
    {
        if (!object.Equals(_value, val.Value))
        {
            _value = val.Value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(StringValue));
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(EffectiveDisplayValue));
        }
        DisplayValue = val.DisplayValue;
        OverrideStatus = val.OverrideStatus;
        Source = val.Source.ToString();
        LastUpdated = val.LastUpdated;
    }

    public Task CommitConfigAsync()
    {
        // Cancel override holds if the user manual tweaks the GenType
        if (_def.OverrideCts != null && _def.Generator?.Type != GenType)
        {
            _def.OverrideCts.Cancel();
            _def.OverrideCts.Dispose();
            _def.OverrideCts = null;
            _def.OriginalGeneratorType = null;
            _store.UpdateOverrideStatus(DeviceId, Key, null);
        }

        // 1. Update Config (Config is bound TwoWay, so mostly already updated, but we ensure persistence)
        if (_def.Type != Type) { _def.Type = Type; }
        
        if (_def.Generator == null) _def.Generator = new GeneratorConfig();
        
        if (_def.Generator.Type != GenType) 
        { 
            _def.Generator.Type = GenType; 
            IsStatic = GenType == "static";
        }
        if (Math.Abs(_def.Generator.Min - GenMin) > 0.001) { _def.Generator.Min = GenMin; }
        if (Math.Abs(_def.Generator.Max - GenMax) > 0.001) { _def.Generator.Max = GenMax; }
        if (Math.Abs(_def.Generator.PeriodSeconds - GenPeriod) > 0.001) { _def.Generator.PeriodSeconds = GenPeriod; }
        if (Math.Abs(_def.Generator.Step - GenStep) > 0.001) { _def.Generator.Step = GenStep; }

        return Task.CompletedTask;
    }

    [RelayCommand]
    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }
}

public partial class DevicePointsGroupViewModel : ObservableObject
{
    private readonly DeviceManager _deviceManager;
    private readonly DeviceInstance _instance;

    public string DeviceId => _instance.Id;
    public string DeviceName => _instance.Name;
    [ObservableProperty] private int _port;

    [ObservableProperty] private string _status;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isExpanded;

    public ObservableCollection<PointViewModel> Points { get; } = new();

    public DevicePointsGroupViewModel(DeviceInstance instance, DeviceManager deviceManager)
    {
        _instance = instance;
        _deviceManager = deviceManager;
        UpdateFromModel();
        IsExpanded = false;
    }

    public void UpdateFromModel()
    {
        Status = _instance.State.ToString();
        Enabled = _instance.Enabled;
        Port = _instance.Network.Port;
    }

    [RelayCommand]
    public async Task Toggle()
    {
        bool isRunning = _instance.State == DeviceInstance.DeviceState.Running;
        WeakReferenceMessenger.Default.Send(new RequestToggleDeviceMessage(_instance.Id, _instance.Name, isRunning));
        UpdateFromModel();
        await Task.CompletedTask;
    }
}
