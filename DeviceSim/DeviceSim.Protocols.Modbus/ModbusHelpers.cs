using global::DeviceSim.Core.Interfaces;
using global::DeviceSim.Core.Models;
using NModbus;
using NModbus.Data;
using System.Net;
using System.Net.Sockets;

namespace DeviceSim.Protocols.Modbus;

public class LinkedDataStore : ISlaveDataStore
{
    public IPointSource<bool> CoilDiscretes { get; }
    public IPointSource<bool> CoilInputs { get; }
    public IPointSource<ushort> HoldingRegisters { get; }
    public IPointSource<ushort> InputRegisters { get; }

    public LinkedDataStore(IPointStore pointStore, DeviceInstance instance)
    {
        // Create mappings: Address -> List of points (for bitfields)
        var coils = new Dictionary<ushort, List<PointDefinition>>();
        var inputs = new Dictionary<ushort, List<PointDefinition>>();
        var holdings = new Dictionary<ushort, List<PointDefinition>>();
        var inputRegs = new Dictionary<ushort, List<PointDefinition>>();

        foreach (var p in instance.Points)
        {
            if (p.Modbus == null) continue;
            ushort addr = p.Modbus.Address; // 0-based internal offset

            Dictionary<ushort, List<PointDefinition>> target;
            switch (p.Modbus.Kind.Trim().ToLower())
            {
                case "coil": target = coils; break;
                case "discrete": target = inputs; break;
                case "holding": target = holdings; break;
                case "input": target = inputRegs; break;
                default: continue;
            }

            if (!target.ContainsKey(addr)) target[addr] = new List<PointDefinition>();
            target[addr].Add(p);
        }

        CoilDiscretes = new ModbusPointSource<bool>(pointStore, instance.Id, coils, PointSource.RemoteWrite);
        CoilInputs = new ModbusPointSource<bool>(pointStore, instance.Id, inputs, PointSource.RemoteWrite);
        HoldingRegisters = new ModbusPointSource<ushort>(pointStore, instance.Id, holdings, PointSource.RemoteWrite);
        InputRegisters = new ModbusPointSource<ushort>(pointStore, instance.Id, inputRegs, PointSource.RemoteWrite);
    }
}

public class ModbusPointSource<T> : IPointSource<T>
{
    private readonly IPointStore _store;
    private readonly string _deviceId;
    private readonly Dictionary<ushort, List<PointDefinition>> _mapping;
    private readonly PointSource _writeSource;

    public ModbusPointSource(IPointStore store, string deviceId, Dictionary<ushort, List<PointDefinition>> mapping, PointSource writeSource)
    {
        _store = store;
        _deviceId = deviceId;
        _mapping = mapping;
        _writeSource = writeSource;
    }


    private T ConvertValue(object value, PointDefinition def)
    {
        double scale = def.Modbus?.Scale ?? 1.0;
        
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)Convert.ToBoolean(value);
        }
        else if (typeof(T) == typeof(ushort))
        {
            try 
            {
                double d = Convert.ToDouble(value);
                ushort rawValue = (ushort)Math.Round(d * scale);

                // Handle BitField (Packed Boolean or Enum)
                if (def.Modbus?.BitField != null)
                {
                    // This is tricky: we are reading a SINGLE point that might be part of a register.
                    // But ReadPoints returns an array where each index is a FULL register.
                    // If multiple points mapped to same register, we need to MERGE them.
                    // HOWEVER, ModbusPointSource.ReadPoints is called by NModbus for a range of addresses.
                    // Our current architecture maps 1 PointDefinition -> 1 Address.
                    // To support "Packed", we need a way to read the OTHER bits of the same register.
                    // For now, let's implement the bit insertion logic.
                    
                    var bf = def.Modbus.BitField;
                    ushort mask = (ushort)(((1 << bf.BitLength) - 1) << bf.StartBit);
                    // Shift value into position and mask it
                    ushort bitValue = (ushort)((((ushort)Math.Round(d)) & ((1 << bf.BitLength) - 1)) << bf.StartBit);
                    
                    // PROBLEM: We don't have the "current" register value here to merge.
                    // We only have the value of THIS point.
                    // TRICK: The LinkedDataStore should probably handle the merging of points sharing the same address.
                    return (T)(object)bitValue; 
                }

                return (T)(object)rawValue;
            } 
            catch { return (T)(object)(ushort)0; }
        }
        return default(T)!;
    }

    private object ConvertFromModbus(T modbusValue, PointDefinition def)
    {
        double scale = def.Modbus?.Scale ?? 1.0;

        if (modbusValue is bool b) return b;
        if (modbusValue is ushort u) 
        {
            // Handle BitField extraction
            if (def.Modbus?.BitField != null)
            {
                var bf = def.Modbus.BitField;
                ushort extracted = (ushort)((u >> bf.StartBit) & ((1 << bf.BitLength) - 1));
                return (double)extracted; // No scale for bitfields usually, or apply scale? Niagara enums are ordinal.
            }
            return (double)u / scale;
        }
        
        return 0;
    }

    public T[] ReadPoints(ushort startAddress, ushort numberOfPoints)
    {
        var result = new T[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            ushort addr = (ushort)(startAddress + i);
            
            if (!_mapping.TryGetValue(addr, out var pointsAtAddr))
            {
                if (typeof(T) == typeof(bool))
                {
                    result[i] = (T)(object)false;
                    continue;
                }
                else
                {
                    throw new IllegalDataAddressException();
                }
            }

            if (typeof(T) == typeof(bool))
            {
                if (pointsAtAddr.Any())
                {
                   var pt = _store.GetValue(_deviceId, pointsAtAddr[0].Key);
                   bool val = Convert.ToBoolean(pt.Value ?? false);
                   result[i] = (T)(object)val;
                }
                else
                {
                    result[i] = (T)(object)false;
                }
            }
            else if (typeof(T) == typeof(ushort))
            {
                ushort finalReg = 0;
                foreach (var def in pointsAtAddr)
                {
                    var pt = _store.GetValue(_deviceId, def.Key);
                    double d = Convert.ToDouble(pt.Value);
                    double scale = def.Modbus?.Scale ?? 1.0;

                    if (def.Modbus?.BitField != null)
                    {
                        var bf = def.Modbus.BitField;
                        ushort bitVal = (ushort)(((ushort)Math.Round(d) & ((1 << bf.BitLength) - 1)) << bf.StartBit);
                        finalReg |= bitVal;
                    }
                    else
                    {
                        finalReg = (ushort)Math.Round(d * scale);
                        break; 
                    }
                }
                result[i] = (T)(object)finalReg;
            }
        }
        return result;
    }

    public void WritePoints(ushort startAddress, T[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            ushort addr = (ushort)(startAddress + i);
            if (_mapping.TryGetValue(addr, out var pointsAtAddr))
            {
                foreach (var def in pointsAtAddr)
                {
                    if (def.Access == AccessMode.Read)
                    {
                        throw new IllegalDataValueException();
                    }

                    var val = ConvertFromModbus(points[i], def);
                    _store.SetValue(_deviceId, def.Key, val, _writeSource);

                    if (def.OverrideMode == ExternalWriteOverrideMode.ForceStatic && def.Generator != null)
                    {
                         def.Generator.Type = "static";
                    }
                }
            }
            else
            {
                throw new IllegalDataAddressException();
            }
        }
    }

}
