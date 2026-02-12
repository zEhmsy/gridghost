using System.Text.Json.Serialization;

namespace DeviceSim.Core.Models;

public class DeviceTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Template";
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProtocolType Protocol { get; set; } = ProtocolType.Modbus;
    
    public NetworkConfig Network { get; set; } = new();
    public BacnetDeviceConfig? Bacnet { get; set; }
    
    public List<PointDefinition> Points { get; set; } = new();
}

public class NetworkConfig
{
    public int Port { get; set; } = 502;
    public string BindIp { get; set; } = "0.0.0.0";
}

public class BacnetDeviceConfig
{
    public uint DeviceInstanceId { get; set; } = 1000;
    public ushort VendorId { get; set; } = 999;
}
