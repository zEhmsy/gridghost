using System.Collections.Concurrent;
using System.Text.Json;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class LogService : ILogSink, IDisposable
{
    public event Action<LogEntry>? OnLog;
    private readonly string _logDirectory;
    private readonly BlockingCollection<LogEntry> _logQueue = new();
    private readonly Task _writeTask;
    private readonly CancellationTokenSource _cts = new();

    public LogService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GridGhost", "logs");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _logDirectory = folder;

        _writeTask = Task.Run(ProcessLogQueue);
    }

    public void Log(string level, string message, string? deviceId = null)
    {
        var entry = new LogEntry(level, message, deviceId);
        OnLog?.Invoke(entry);
        _logQueue.Add(entry);
    }

    public void LogException(Exception ex, string message, string? deviceId = null)
    {
        var entry = new LogEntry("Error", $"{message}: {ex.Message}", deviceId);
        OnLog?.Invoke(entry);
        _logQueue.Add(entry);
    }

    private async Task ProcessLogQueue()
    {
        foreach (var entry in _logQueue.GetConsumingEnumerable(_cts.Token))
        {
            try
            {
                var filename = $"log-{DateTime.Now:yyyy-MM-dd}.jsonl";
                var path = Path.Combine(_logDirectory, filename);
                var json = JsonSerializer.Serialize(entry);
                await File.AppendAllTextAsync(path, json + Environment.NewLine, _cts.Token);
            }
            catch
            {
                // Last resort fallback? Console?
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _logQueue.CompleteAdding();
        try
        {
            _writeTask.Wait(1000);
        }
        catch { }
        _cts.Dispose();
    }
}
