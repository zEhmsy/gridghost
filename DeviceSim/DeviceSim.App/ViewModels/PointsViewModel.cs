using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Avalonia.Threading;

namespace DeviceSim.App.ViewModels;

public partial class PointsViewModel : ViewModelBase
{
    private readonly IPointStore _pointStore;
    private readonly DeviceManager _deviceManager;

    public ObservableCollection<PointViewModel> Points { get; } = new();

    [ObservableProperty]
    private string _filterText = "";

    public PointsViewModel(IPointStore pointStore, DeviceManager deviceManager)
    {
        _pointStore = pointStore;
        _deviceManager = deviceManager;
        _pointStore.OnPointChanged += OnPointChanged;
        _deviceManager.OnDeviceUpdated += OnDeviceUpdated;
        _deviceManager.OnDeviceRemoved += OnDeviceRemoved;
        
        RefreshPoints();
    }

    private void RefreshPoints()
    {
        Points.Clear();
        var devices = _deviceManager.GetAll().ToList();
        foreach (var device in devices)
        {
            foreach (var pointDef in device.Points)
            {
                if (_pointStore.TryGetValue(device.Id, pointDef.Key, out var val))
                {
                    Points.Add(new PointViewModel(device, pointDef, val, _pointStore, _deviceManager));
                }
            }
        }
    }

    private void OnDeviceRemoved(string deviceId)
    {
        Dispatcher.UIThread.Post(() => {
            var toRemove = Points.Where(p => p.DeviceId == deviceId).ToList();
            foreach (var p in toRemove) Points.Remove(p);
        });
    }

    private void OnDeviceUpdated(DeviceInstance instance)
    {
        Dispatcher.UIThread.Post(RefreshPoints); // Brute force refresh for simplicity
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
    [ObservableProperty] private object _value;
    [ObservableProperty] private string? _displayValue;
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
    [ObservableProperty] private bool _isEditingAllowed;
    [ObservableProperty] private bool _isStatic;

    // Derived property for UI to avoid blanking when Store sends empty value
    public string EffectiveDisplayValue 
    {
        get
        {
            if (Value == null) return "-";
            
            if (Value is bool b) return b ? "True" : "False";
            
            if (Value is double d) return d.ToString("F2");
            if (Value is float f) return f.ToString("F2");
            
            return Value.ToString() ?? "";
        }
    }

    partial void OnValueChanged(object value)
    {
        OnPropertyChanged(nameof(EffectiveDisplayValue));
    }

    partial void OnGenTypeChanged(string value) => IsStatic = value == "static";

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
        
        _isStatic = _genType == "static";
        _isEditingAllowed = _device.State != DeviceInstance.DeviceState.Running;
    }

    public void Update(PointValue val)
    {
        if (!object.Equals(Value, val.Value)) Value = val.Value;
        DisplayValue = val.DisplayValue;
        Source = val.Source.ToString();
        LastUpdated = val.LastUpdated;
    }

    [RelayCommand]
    public async Task SetValue(object? input)
    {
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

        // 2. Set Value ONLY if Static
        if (IsStatic && input != null)
        {
            object val = input;
            if (input is string str)
            {
                if (double.TryParse(str, out double d)) val = d;
                else if (bool.TryParse(str, out bool b)) val = b;
            }
            
            // Generate display value string to prevent UI blanking
            string disp = val.ToString() ?? "";
            if (val is double dv) disp = dv.ToString("F2");
            
             _store.SetValue(DeviceId, Key, val, PointSource.Manual, disp);
        }
        
        await Task.CompletedTask;
    }
}
