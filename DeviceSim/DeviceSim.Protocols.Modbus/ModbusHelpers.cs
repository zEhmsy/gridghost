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

            int numRegs = 1;
            if (p.Modbus.Kind.Trim().ToLower() == "holding" || p.Modbus.Kind.Trim().ToLower() == "input")
            {
                if (p.Type == "int32" || p.Type == "uint32" || p.Type == "float") 
                    numRegs = 2;
            }

            for (int i = 0; i < numRegs; i++)
            {
                ushort a = (ushort)(addr + i);
                if (!target.ContainsKey(a)) target[a] = new List<PointDefinition>();
                target[a].Add(p);
            }
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
                        // Handle 32-bit spanning
                        if (def.Type == "float" || def.Type == "int32" || def.Type == "uint32")
                        {
                            int offset = addr - def.Modbus!.Address;
                            ushort word0, word1;
                            
                            if (def.Type == "float")
                            {
                                byte[] b = BitConverter.GetBytes((float)(d * scale));
                                word0 = (ushort)(b[0] | (b[1] << 8));
                                word1 = (ushort)(b[2] | (b[3] << 8));
                            }
                            else if (def.Type == "int32")
                            {
                                byte[] b = BitConverter.GetBytes((int)Math.Round(d * scale));
                                word0 = (ushort)(b[0] | (b[1] << 8));
                                word1 = (ushort)(b[2] | (b[3] << 8));
                            }
                            else // uint32
                            {
                                byte[] b = BitConverter.GetBytes((uint)Math.Round(d * scale));
                                word0 = (ushort)(b[0] | (b[1] << 8));
                                word1 = (ushort)(b[2] | (b[3] << 8));
                            }

                            // Modbus typically sends high word first in 32-bit unless swapped. 
                            // CDAB or ABCD. Let's assume standard ABCD (High Word first)
                            // Wait, ABCD would mean word1 is at addr, word0 is at addr+1
                            // Modbus standard for 32-bit floats is often Big-Endian (ABCD)
                            finalReg = offset == 0 ? word1 : word0;
                        }
                        else 
                        {
                            // 16-bit
                            if (def.Type == "uint16")
                                finalReg = (ushort)Math.Round(d * scale);
                            else
                                finalReg = (ushort)(short)Math.Round(d * scale);
                        }
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

                    object val;
                    if (typeof(T) == typeof(ushort) && (def.Type == "float" || def.Type == "int32" || def.Type == "uint32"))
                    {
                        var pt = _store.GetValue(_deviceId, def.Key);
                        double currentVal = Convert.ToDouble(pt.Value ?? 0);
                        double scale = def.Modbus?.Scale ?? 1.0;
                        
                        ushort word0, word1;
                        if (def.Type == "float") {
                            byte[] b = BitConverter.GetBytes((float)(currentVal * scale));
                            word0 = (ushort)(b[0] | (b[1] << 8));
                            word1 = (ushort)(b[2] | (b[3] << 8));
                        } else if (def.Type == "int32") {
                            byte[] b = BitConverter.GetBytes((int)Math.Round(currentVal * scale));
                            word0 = (ushort)(b[0] | (b[1] << 8));
                            word1 = (ushort)(b[2] | (b[3] << 8));
                        } else {
                            byte[] b = BitConverter.GetBytes((uint)Math.Round(currentVal * scale));
                            word0 = (ushort)(b[0] | (b[1] << 8));
                            word1 = (ushort)(b[2] | (b[3] << 8));
                        }
                        
                        int offset = addr - def.Modbus!.Address;
                        ushort newReg = (ushort)(object)points[i];
                        
                        // ABCD Big -Endien Default Setup
                        if (offset == 0) word1 = newReg; else word0 = newReg;
                        
                        byte[] newBytes = new byte[4];
                        newBytes[0] = (byte)(word0 & 0xFF);
                        newBytes[1] = (byte)(word0 >> 8);
                        newBytes[2] = (byte)(word1 & 0xFF);
                        newBytes[3] = (byte)(word1 >> 8);
                        
                        if (def.Type == "float") 
                            val = (double)BitConverter.ToSingle(newBytes, 0) / scale;
                        else if (def.Type == "int32") 
                            val = (double)BitConverter.ToInt32(newBytes, 0) / scale;
                        else 
                            val = (double)BitConverter.ToUInt32(newBytes, 0) / scale;
                    }
                    else
                    {
                        val = ConvertFromModbus(points[i], def);
                    }

                    _store.SetValue(_deviceId, def.Key, val, _writeSource);

                    if (def.Generator != null)
                    {
                        if (def.OverrideMode == ExternalWriteOverrideMode.ForceStatic)
                        {
                            def.Generator.Type = "static";
                            if (def.OverrideCts != null)
                            {
                                def.OverrideCts.Cancel();
                                def.OverrideCts.Dispose();
                                def.OverrideCts = null;
                            }
                            _store.UpdateOverrideStatus(_deviceId, def.Key, null);
                        }
                        else if (def.OverrideMode == ExternalWriteOverrideMode.HoldForSeconds)
                        {
                            if (def.OverrideCts != null)
                            {
                                def.OverrideCts.Cancel();
                                def.OverrideCts.Dispose();
                            }

                            if (def.OriginalGeneratorType == null || def.Generator.Type != "static")
                                def.OriginalGeneratorType = def.Generator.Type;

                            def.Generator.Type = "static";
                            
                            var cts = new System.Threading.CancellationTokenSource();
                            def.OverrideCts = cts;
                            int duration = def.OverrideDurationSeconds > 0 ? def.OverrideDurationSeconds : 10;
                            string original = def.OriginalGeneratorType;

                            Task.Run(async () =>
                            {
                                try
                                {
                                    int remaining = duration;
                                    while (remaining > 0)
                                    {
                                        _store.UpdateOverrideStatus(_deviceId, def.Key, $"Override ({remaining}s)");
                                        await Task.Delay(1000, cts.Token);
                                        remaining--;
                                    }

                                    if (def.Generator.Type == "static") 
                                        def.Generator.Type = original;
                                        
                                    def.OriginalGeneratorType = null;
                                    def.OverrideCts = null;
                                    _store.UpdateOverrideStatus(_deviceId, def.Key, null);
                                }
                                catch (TaskCanceledException) { }
                            });
                        }
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
