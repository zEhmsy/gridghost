using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DeviceSim.Core.Models;

namespace DeviceSim.App.ViewModels;

public partial class PointEditorViewModel : ObservableObject
{
    private readonly PointDefinition _point;

    public PointEditorViewModel(PointDefinition point)
    {
        _point = point;
        // Ensure Modbus config exists for binding
        if (_point.Modbus == null)
        {
            _point.Modbus = new ModbusPointConfig();
        }
        if (_point.Generator == null)
        {
            _point.Generator = new GeneratorConfig();
        }
    }

    public PointDefinition Point => _point;

    public string Key
    {
        get => _point.Key;
        set { 
            if (_point.Key != value) {
                _point.Key = value;
                OnPropertyChanged();
            }
        }
    }

    public string Type
    {
        get => _point.Type;
        set {
            if (_point.Type != value) {
                _point.Type = value;
                OnPropertyChanged();
            }
        }
    }
    
    // Modbus Wrappers
    public string ModbusKind
    {
        get => _point.Modbus?.Kind ?? "Holding";
        set {
            if (_point.Modbus == null) _point.Modbus = new ModbusPointConfig();
            if (_point.Modbus.Kind != value) {
                _point.Modbus.Kind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NiagaraAddressPreview));
            }
        }
    }

    public ushort ModbusAddress
    {
        get => _point.Modbus?.Address ?? 0;
        set {
            if (_point.Modbus == null) _point.Modbus = new ModbusPointConfig();
            if (_point.Modbus.Address != value) {
                _point.Modbus.Address = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NiagaraAddressPreview));
            }
        }
    }

    public string NiagaraAddressPreview
    {
        get
        {
            int offset = ModbusAddress; // 0-based in JSON
            // Determine prefix
            string kind = ModbusKind?.ToLower() ?? "holding";
            
            // Standard Modbus usually 1-based for display? Or 0-based.
            // Niagara Convention:
            // Coil (0xxxx) -> 00001 + offset
            // Discrete (1xxxx) -> 10001 + offset
            // Input (3xxxx) -> 30001 + offset
            // Holding (4xxxx) -> 40001 + offset
            
            int baseAddr = 1 + offset; 

            return kind switch
            {
                "coil" => $"0{baseAddr:D4}",
                "discrete" => $"1{baseAddr:D4}",
                "input" => $"3{baseAddr:D4}",
                _ => $"4{baseAddr:D4}" // holding
            };
        }
    }

    public double Scale
    {
        get => _point.Modbus?.Scale ?? 1.0;
        set {
            if (_point.Modbus == null) _point.Modbus = new ModbusPointConfig();
            if (Math.Abs(_point.Modbus.Scale - value) > 0.0001) {
                _point.Modbus.Scale = value;
                OnPropertyChanged();
            }
        }
    }

    public AccessMode Access
    {
        get => _point.Access;
        set {
            if (_point.Access != value) {
                _point.Access = value;
                OnPropertyChanged();
            }
        }
    }

    // Generator Wrappers
    public string GeneratorType
    {
        get => _point.Generator?.Type ?? "static";
        set {
             if (_point.Generator == null) _point.Generator = new GeneratorConfig();
             if (_point.Generator.Type != value) {
                 _point.Generator.Type = value;
                 OnPropertyChanged();
             }
        }
    }

    public ExternalWriteOverrideMode OverrideMode
    {
        get => _point.OverrideMode;
        set {
            if (_point.OverrideMode != value) {
                _point.OverrideMode = value;
                OnPropertyChanged();
            }
        }
    }

    public static List<string> PointTypes { get; } = new() { "bool", "int16", "uint16", "float", "int32", "uint32" };
    public static List<string> ModbusKinds { get; } = new() { "Holding", "Input", "Coil", "Discrete" };
    public static List<AccessMode> AccessModes { get; } = Enum.GetValues<AccessMode>().ToList();
    public static List<string> GeneratorTypes { get; } = new() { "static", "ramp", "sine", "random" };
    public static List<ExternalWriteOverrideMode> OverrideModes { get; } = Enum.GetValues<ExternalWriteOverrideMode>().ToList();
}
