using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceSim.Core.Models;

public class DeviceInstance
{
    public enum DeviceState
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Faulted
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = "Device 1";
    
    // Copy of configuration to allow override
    public ProtocolType Protocol { get; set; } = ProtocolType.Modbus;
    public NetworkConfig Network { get; set; } = new();
    public BacnetDeviceConfig? Bacnet { get; set; }
    public List<PointDefinition> Points { get; set; } = new();
    
    // Runtime state (not persisted)
    [System.Text.Json.Serialization.JsonIgnore]
    public DeviceState State { get; set; } = DeviceState.Stopped;
    public bool Enabled { get; set; } // Desired state
    [System.Text.Json.Serialization.JsonIgnore]
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
            Points = template.Points.Select(p => new PointDefinition
            {
                Key = p.Key,
                Name = p.Name,
                Type = p.Type,
                NiagaraType = p.NiagaraType,
                Unit = p.Unit,
                Access = p.Access,
                OverrideMode = p.OverrideMode,
                Modbus = p.Modbus == null ? null : new ModbusPointConfig
                {
                    Address = p.Modbus.Address,
                    Kind = p.Modbus.Kind,
                    Scale = p.Modbus.Scale,
                    BitField = p.Modbus.BitField == null ? null : new BitFieldConfig
                    {
                        StartBit = p.Modbus.BitField.StartBit,
                        BitLength = p.Modbus.BitField.BitLength
                    }
                },
                Generator = p.Generator == null ? new GeneratorConfig() : new GeneratorConfig
                {
                    Type = p.Generator.Type,
                    Min = p.Generator.Min,
                    Max = p.Generator.Max,
                    PeriodSeconds = p.Generator.PeriodSeconds
                }
            }).ToList()
        };
    }
}
