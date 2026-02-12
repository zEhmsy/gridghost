using global::DeviceSim.Core.Interfaces;
using global::DeviceSim.Core.Models;
using NModbus;
using System.Net;
using System.Net.Sockets;
using NModbus.Data;

namespace DeviceSim.Protocols.Modbus;

public class LinkedDataStore : ISlaveDataStore
{
    public IPointSource<bool> CoilDiscretes { get; }
    public IPointSource<bool> CoilInputs { get; }
    public IPointSource<ushort> HoldingRegisters { get; }
    public IPointSource<ushort> InputRegisters { get; }

    public LinkedDataStore(IPointStore pointStore, DeviceInstance instance)
    {
        // Create mappings
        var coils = new Dictionary<ushort, PointDefinition>();
        var inputs = new Dictionary<ushort, PointDefinition>();
        var holdings = new Dictionary<ushort, PointDefinition>();
        var inputRegs = new Dictionary<ushort, PointDefinition>();

        foreach (var p in instance.Points)
        {
            if (p.Modbus == null) continue;
            // NModbus uses 1-based addressing usually, but let's stick to 0-based from config 
            // and handle offset in source if needed. 
            // NModbus `ReadPoints` passes startAddress.
            
            // Note: NModbus typically passes 1-based address if using default, but let's assume raw address.
            // Actually NModbus DataStore usually handles 1..65535. 
            // In ModbusAdapter we were doing `p.Modbus.Address + 1`. 
            // Let's assume the PointSource receives the raw address requested by master.
            
            ushort addr = (ushort)(p.Modbus.Address + 1);

            switch (p.Modbus.Kind.ToLower())
            {
                case "coil": coils[addr] = p; break;
                case "discrete": inputs[addr] = p; break;
                case "holding": holdings[addr] = p; break;
                case "input": inputRegs[addr] = p; break;
            }
        }

        CoilDiscretes = new ModbusPointSource<bool>(pointStore, instance.Id, coils, PointSource.RemoteWrite);
        CoilInputs = new ModbusPointSource<bool>(pointStore, instance.Id, inputs, PointSource.RemoteWrite); // Read-only usually
        HoldingRegisters = new ModbusPointSource<ushort>(pointStore, instance.Id, holdings, PointSource.RemoteWrite);
        InputRegisters = new ModbusPointSource<ushort>(pointStore, instance.Id, inputRegs, PointSource.RemoteWrite); // Read-only
    }
}

public class ModbusPointSource<T> : IPointSource<T>
{
    private readonly IPointStore _store;
    private readonly string _deviceId;
    private readonly Dictionary<ushort, PointDefinition> _mapping;
    private readonly PointSource _writeSource;

    public ModbusPointSource(IPointStore store, string deviceId, Dictionary<ushort, PointDefinition> mapping, PointSource writeSource)
    {
        _store = store;
        _deviceId = deviceId;
        _mapping = mapping;
        _writeSource = writeSource;
    }

    public T[] ReadPoints(ushort startAddress, ushort numberOfPoints)
    {
        var result = new T[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            ushort addr = (ushort)(startAddress + i);
            if (_mapping.TryGetValue(addr, out var def))
            {
                var pt = _store.GetValue(_deviceId, def.Key);
                result[i] = ConvertValue(pt.Value, def);
            }
            else
            {
                result[i] = default(T)!;
            }
        }
        return result;
    }

    public void WritePoints(ushort startAddress, T[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            ushort addr = (ushort)(startAddress + i);
            if (_mapping.TryGetValue(addr, out var def))
            {
                var val = ConvertFromModbus(points[i], def);
                _store.SetValue(_deviceId, def.Key, val, _writeSource);
            }
        }
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
                return (T)(object)(ushort)(d * scale);
            } 
            catch { return (T)(object)(ushort)0; }
        }
        return default(T)!;
    }

    private object ConvertFromModbus(T modbusValue, PointDefinition def)
    {
        double scale = def.Modbus?.Scale ?? 1.0;

        if (modbusValue is bool b) return b;
        if (modbusValue is ushort u) return (double)u / scale;
        
        return 0;
    }
}
