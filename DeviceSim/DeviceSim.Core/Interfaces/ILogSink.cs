using DeviceSim.Core.Models;

namespace DeviceSim.Core.Interfaces;

public interface ILogSink
{
    void Log(string level, string message, string? deviceId = null);
    void LogException(Exception ex, string message, string? deviceId = null);
    
    event Action<LogEntry> OnLog;
}
