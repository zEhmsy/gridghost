using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using System;

namespace DeviceSim.Core.Services;

public class LogService : ILogSink
{
    public event Action<LogEntry>? OnLog;

    public void Log(string level, string message, string? deviceId = null)
    {
        OnLog?.Invoke(new LogEntry(level, message, deviceId));
    }

    public void LogException(Exception ex, string message, string? deviceId = null)
    {
        OnLog?.Invoke(new LogEntry("Error", $"{message}: {ex.Message}", deviceId));
    }
}
