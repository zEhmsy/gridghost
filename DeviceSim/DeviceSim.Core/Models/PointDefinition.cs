using System.Text.Json.Serialization;

namespace DeviceSim.Core.Models;

public class PointDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "float"; // bool, int16, uint16, int32, uint32, float
    
    // Niagara-specific metadata
    public string NiagaraType { get; set; } = "Numeric"; // Boolean, Numeric, Enum
    public string? Unit { get; set; }

    // Generator config (optional)
    public GeneratorConfig? Generator { get; set; }

    // Protocol specific mappings
    public ModbusPointConfig? Modbus { get; set; }
    public BacnetPointConfig? Bacnet { get; set; }

    // Access Control & Override
    public AccessMode Access { get; set; } = AccessMode.ReadWrite;
    public ExternalWriteOverrideMode OverrideMode { get; set; } = ExternalWriteOverrideMode.None;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessMode
{
    Read,
    Write,
    ReadWrite
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalWriteOverrideMode
{
    None,
    ForceStatic,
    HoldForSeconds
}

public class GeneratorConfig
{
    public string Type { get; set; } = "static"; // static, ramp, sine, random
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double PeriodSeconds { get; set; } = 10;
    public double Step { get; set; } = 1;
}

public class ModbusPointConfig
{
    public string Kind { get; set; } = "holding"; // holding, input, coil, discrete
    public ushort Address { get; set; }
    public double Scale { get; set; } = 1.0;
    public BitFieldConfig? BitField { get; set; }
    public List<EnumMapEntry>? EnumMapping { get; set; }
}

public class BitFieldConfig
{
    public int StartBit { get; set; }
    public int BitLength { get; set; } = 1;
}

public class EnumMapEntry
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class BacnetPointConfig
{
    public string ObjectType { get; set; } = "analogValue"; // analogValue, binaryValue, etc.
    public uint Instance { get; set; }
    public string Property { get; set; } = "presentValue";
}
