using System.Collections.Concurrent;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class SimulationScheduler : IDisposable
{
    private readonly IPointStore _pointStore;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _deviceCts = new();
    private readonly ConcurrentDictionary<string, Task> _deviceTasks = new();
    private int _intervalMs = 500;

    public SimulationScheduler(IPointStore pointStore)
    {
        _pointStore = pointStore;
    }

    public void StartDeviceSimulation(DeviceInstance device)
    {
        if (_deviceCts.ContainsKey(device.Id)) return;

        var cts = new CancellationTokenSource();
        _deviceCts[device.Id] = cts;

        var task = Task.Run(async () => 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    UpdateDevicePoints(device);
                    await Task.Delay(_intervalMs, cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { /* Log error if needed */ }
            }
        }, cts.Token);

        _deviceTasks[device.Id] = task;
    }

    public async Task StopDeviceSimulationAsync(string deviceId)
    {
        if (_deviceCts.TryRemove(deviceId, out var cts))
        {
            cts.Cancel();
            if (_deviceTasks.TryRemove(deviceId, out var task))
            {
                try { await task; } catch { }
            }
            cts.Dispose();
        }
    }

    private void UpdateDevicePoints(DeviceInstance device)
    {
        foreach (var point in device.Points)
        {
            if (point.Generator != null && point.Generator.Type != "static")
            {
                UpdatePointValue(device.Id, point);
            }
        }
    }

    private void UpdatePointValue(string deviceId, PointDefinition point)
    {
        if (point.Generator == null) return;

        var gen = point.Generator;
        double newValue = 0;
        double time = (DateTime.Now.Ticks / 10000.0) / 1000.0; // Seconds

        switch (gen.Type.ToLower())
        {
            case "ramp":
                double range = gen.Max - gen.Min;
                if (range == 0) range = 1;
                double progress = (time % gen.PeriodSeconds) / gen.PeriodSeconds;
                newValue = gen.Min + (progress * range);
                break;
            case "sine":
                double mid = (gen.Max + gen.Min) / 2.0;
                double amp = (gen.Max - gen.Min) / 2.0;
                newValue = mid + amp * Math.Sin(2 * Math.PI * time / gen.PeriodSeconds);
                break;
            case "random":
                newValue = gen.Min + (Random.Shared.NextDouble() * (gen.Max - gen.Min));
                break;
        }

        // Force boolean rounding if type is bool, to ensure 0 or 1 in store
        if (point.Type == "bool")
        {
            newValue = newValue >= 0.5 ? 1.0 : 0.0;
        }

        string? displayValue = null;
        if (point.NiagaraType == "Enum" && point.Modbus?.EnumMapping != null)
        {
            int intVal = (int)Math.Round(newValue);
            displayValue = point.Modbus.EnumMapping.FirstOrDefault(e => e.Value == intVal)?.Label ?? intVal.ToString();
        }
        else if (point.NiagaraType == "Numeric")
        {
            displayValue = newValue.ToString("F2");
        }

        _pointStore.SetValue(deviceId, point.Key, newValue, PointSource.Simulation, displayValue);
    }

    public void Dispose()
    {
        foreach (var cts in _deviceCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
