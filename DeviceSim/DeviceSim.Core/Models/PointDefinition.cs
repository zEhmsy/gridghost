using System.Text.Json.Serialization;

namespace DeviceSim.Core.Models;

public class PointDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = "float"; // bool, int16, uint16, int32, uint32, float, float32
    
    // Generator config (optional)
    public GeneratorConfig? Generator { get; set; }

    // Protocol specific mappings
    public ModbusPointConfig? Modbus { get; set; }
    public BacnetPointConfig? Bacnet { get; set; }
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
}

public class BacnetPointConfig
{
    public string ObjectType { get; set; } = "analogValue"; // analogValue, binaryValue, etc.
    public uint Instance { get; set; }
    public string Property { get; set; } = "presentValue";
}
