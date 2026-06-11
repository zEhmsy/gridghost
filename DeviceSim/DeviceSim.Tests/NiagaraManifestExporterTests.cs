using System.Text.Json;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Xunit;

namespace DeviceSim.Tests;

public class NiagaraManifestExporterTests
{
    [Fact]
    public void BuildManifest_ExportsModbusDeviceMetadataAndAddresses()
    {
        var device = new DeviceInstance
        {
            Id = "dev-1",
            Name = "AHU 1",
            Protocol = ProtocolType.Modbus,
            Network = new NetworkConfig
            {
                BindIp = "0.0.0.0",
                Port = 1502,
                DeviceAddress = 7
            },
            Points =
            {
                new PointDefinition
                {
                    Key = "supply_temp",
                    Name = "Supply Temp",
                    Type = "int16",
                    NiagaraType = "Numeric",
                    Unit = "degC",
                    Access = AccessMode.Read,
                    Modbus = new ModbusPointConfig
                    {
                        Kind = "Holding",
                        Address = 0,
                        Scale = 10
                    }
                },
                new PointDefinition
                {
                    Key = "fan_status",
                    Name = "Fan Status",
                    Type = "bool",
                    NiagaraType = "Boolean",
                    Access = AccessMode.ReadWrite,
                    Modbus = new ModbusPointConfig
                    {
                        Kind = "Coil",
                        Address = 4
                    }
                }
            }
        };

        var manifest = NiagaraManifestExporter.BuildManifest(
            new[] { device },
            new NiagaraExportConfig { ExportHost = "192.168.10.50" });

        Assert.Equal(NiagaraManifestExporter.Schema, manifest.Schema);
        Assert.Equal("192.168.10.50", manifest.ExportHost);
        var exportedDevice = Assert.Single(manifest.Devices);
        Assert.Equal("AHU 1", exportedDevice.Name);
        Assert.Equal("192.168.10.50", exportedDevice.Host);
        Assert.Equal(1502, exportedDevice.Port);
        Assert.Equal(7, exportedDevice.DeviceAddress);
        Assert.Equal(2, exportedDevice.Points.Count);
        Assert.Contains(exportedDevice.Points, p => p.Key == "fan_status" && p.Modbus.NiagaraAddress == "00005");
        Assert.Contains(exportedDevice.Points, p => p.Key == "supply_temp" && p.Modbus.NiagaraAddress == "40001");
    }

    [Fact]
    public void Export_WritesValidJsonFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gridghost-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(tempDir, "niagara-manifest.json");
        var device = new DeviceInstance
        {
            Id = "dev-2",
            Name = "Meter",
            Protocol = ProtocolType.Modbus,
            Network = new NetworkConfig { Port = 1503, DeviceAddress = 2 },
            Points =
            {
                new PointDefinition
                {
                    Key = "kwh",
                    Name = "kWh",
                    Type = "uint32",
                    Modbus = new ModbusPointConfig { Kind = "Input", Address = 10 }
                }
            }
        };

        try
        {
            var result = NiagaraManifestExporter.Export(
                new[] { device },
                new NiagaraExportConfig
                {
                    ExportHost = "10.0.0.20",
                    ExportPath = path
                });

            Assert.Equal(path, result.Path);
            Assert.True(File.Exists(path));
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(NiagaraManifestExporter.Schema, document.RootElement.GetProperty("schema").GetString());
            Assert.Equal("10.0.0.20", document.RootElement.GetProperty("exportHost").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
