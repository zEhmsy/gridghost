using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public sealed record NiagaraManifestExportResult(string Path, int DeviceCount, int PointCount);

public static class NiagaraManifestExporter
{
    public const string Schema = "gridghost.niagara.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static NiagaraManifestExportResult Export(
        IEnumerable<DeviceInstance> devices,
        NiagaraExportConfig config)
    {
        var path = string.IsNullOrWhiteSpace(config.ExportPath)
            ? NiagaraExportConfig.GetDefaultExportPath()
            : config.ExportPath;

        var manifest = BuildManifest(devices, config);
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(tempPath, path, true);

        return new NiagaraManifestExportResult(
            path,
            manifest.Devices.Count,
            manifest.Devices.Sum(d => d.Points.Count));
    }

    public static NiagaraManifest BuildManifest(
        IEnumerable<DeviceInstance> devices,
        NiagaraExportConfig config)
    {
        var exportHost = string.IsNullOrWhiteSpace(config.ExportHost)
            ? "127.0.0.1"
            : config.ExportHost.Trim();

        var manifestDevices = devices
            .Where(d => d.Protocol == ProtocolType.Modbus)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => BuildDevice(d, exportHost))
            .Where(d => d.Points.Count > 0)
            .ToList();

        return new NiagaraManifest
        {
            Schema = Schema,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ExportHost = exportHost,
            Devices = manifestDevices
        };
    }

    private static NiagaraManifestDevice BuildDevice(DeviceInstance device, string exportHost)
    {
        var points = device.Points
            .Where(p => p.Modbus != null)
            .OrderBy(p => NormalizeKind(p.Modbus!.Kind))
            .ThenBy(p => p.Modbus!.Address)
            .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(BuildPoint)
            .ToList();

        return new NiagaraManifestDevice
        {
            Id = device.Id,
            Name = device.Name,
            Protocol = device.Protocol.ToString(),
            Host = exportHost,
            BindIp = device.Network.BindIp,
            Port = device.Network.Port,
            DeviceAddress = device.Network.DeviceAddress,
            Enabled = device.Enabled,
            Points = points
        };
    }

    private static NiagaraManifestPoint BuildPoint(PointDefinition point)
    {
        var modbus = point.Modbus!;
        var kind = NormalizeKind(modbus.Kind);
        var niagaraType = string.Equals(point.Type, "bool", StringComparison.OrdinalIgnoreCase)
            ? "Boolean"
            : NormalizeNiagaraType(point.NiagaraType);

        return new NiagaraManifestPoint
        {
            Key = point.Key,
            Name = string.IsNullOrWhiteSpace(point.Name) ? point.Key : point.Name,
            NiagaraType = niagaraType,
            DataType = point.Type,
            Unit = string.IsNullOrWhiteSpace(point.Unit) ? null : point.Unit,
            Access = point.Access.ToString(),
            Modbus = new NiagaraManifestModbus
            {
                Kind = kind,
                Address = modbus.Address,
                NiagaraAddress = FormatNiagaraAddress(kind, modbus.Address),
                Scale = modbus.Scale,
                BitField = modbus.BitField == null ? null : new NiagaraManifestBitField
                {
                    StartBit = modbus.BitField.StartBit,
                    BitLength = modbus.BitField.BitLength
                },
                EnumMapping = modbus.EnumMapping?
                    .OrderBy(e => e.Value)
                    .Select(e => new NiagaraManifestEnumEntry
                    {
                        Value = e.Value,
                        Label = e.Label
                    })
                    .ToList()
            },
            GeneratorType = point.Generator?.Type
        };
    }

    public static string FormatNiagaraAddress(string kind, ushort address)
    {
        var prefix = NormalizeKind(kind) switch
        {
            "Coil" => "0",
            "Discrete" => "1",
            "Input" => "3",
            "Holding" => "4",
            _ => string.Empty
        };

        return string.IsNullOrEmpty(prefix)
            ? address.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{prefix}{address + 1:D4}";
    }

    private static string NormalizeKind(string? kind)
    {
        return (kind ?? "Holding").Trim().ToLowerInvariant() switch
        {
            "coil" => "Coil",
            "discrete" => "Discrete",
            "input" => "Input",
            "holding" => "Holding",
            _ => "Holding"
        };
    }

    private static string NormalizeNiagaraType(string? niagaraType)
    {
        return (niagaraType ?? "Numeric").Trim().ToLowerInvariant() switch
        {
            "boolean" => "Boolean",
            "enum" => "Enum",
            _ => "Numeric"
        };
    }
}

public sealed class NiagaraManifest
{
    public string Schema { get; set; } = NiagaraManifestExporter.Schema;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string ExportHost { get; set; } = "127.0.0.1";
    public List<NiagaraManifestDevice> Devices { get; set; } = new();
}

public sealed class NiagaraManifestDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "Modbus";
    public string Host { get; set; } = "127.0.0.1";
    public string BindIp { get; set; } = "0.0.0.0";
    public int Port { get; set; }
    public byte DeviceAddress { get; set; } = 1;
    public bool Enabled { get; set; }
    public List<NiagaraManifestPoint> Points { get; set; } = new();
}

public sealed class NiagaraManifestPoint
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NiagaraType { get; set; } = "Numeric";
    public string DataType { get; set; } = "float";
    public string? Unit { get; set; }
    public string Access { get; set; } = AccessMode.ReadWrite.ToString();
    public NiagaraManifestModbus Modbus { get; set; } = new();
    public string? GeneratorType { get; set; }
}

public sealed class NiagaraManifestModbus
{
    public string Kind { get; set; } = "Holding";
    public ushort Address { get; set; }
    public string NiagaraAddress { get; set; } = "40001";
    public double Scale { get; set; } = 1.0;
    public NiagaraManifestBitField? BitField { get; set; }
    public List<NiagaraManifestEnumEntry>? EnumMapping { get; set; }
}

public sealed class NiagaraManifestBitField
{
    public int StartBit { get; set; }
    public int BitLength { get; set; } = 1;
}

public sealed class NiagaraManifestEnumEntry
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
}
