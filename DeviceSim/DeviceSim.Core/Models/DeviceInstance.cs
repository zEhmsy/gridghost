namespace DeviceSim.Core.Models;

public class DeviceInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    // Copy of configuration to allow override
    public ProtocolType Protocol { get; set; }
    public NetworkConfig Network { get; set; } = new();
    public BacnetDeviceConfig? Bacnet { get; set; }
    public List<PointDefinition> Points { get; set; } = new();

    // Runtime state
    public bool Enabled { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Stopped;
    public string? LastError { get; set; }

    // Helper to create from template
    public static DeviceInstance FromTemplate(DeviceTemplate template)
    {
        return new DeviceInstance
        {
            TemplateId = template.Id,
            Name = $"{template.Name} Instance",
            Protocol = template.Protocol,
            Network = new NetworkConfig 
            { 
                Port = template.Network.Port, 
                BindIp = template.Network.BindIp 
            },
            Bacnet = template.Bacnet != null ? new BacnetDeviceConfig 
            { 
                DeviceInstanceId = template.Bacnet.DeviceInstanceId,
                VendorId = template.Bacnet.VendorId
            } : null,
            Points = template.Points.Select(p => p).ToList() // Shallow copy of points definition list
        };
    }
}
