namespace DeviceSim.Core.Models;

public class PointValue
{
    public object Value { get; set; } = 0;
    public string ExpectedType { get; set; } = "float";
    public string? DisplayValue { get; set; } // Formatted value or enum label
    public string? OverrideStatus { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public PointSource Source { get; set; } = PointSource.Manual;
}

public enum PointSource
{
    Manual,
    Simulation,
    RemoteWrite
}
