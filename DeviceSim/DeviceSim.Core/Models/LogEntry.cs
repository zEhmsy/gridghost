namespace DeviceSim.Core.Models;

public record LogEntry(string Level, string Message, string? DeviceId)
{
    public DateTime Timestamp { get; } = DateTime.Now;
}
