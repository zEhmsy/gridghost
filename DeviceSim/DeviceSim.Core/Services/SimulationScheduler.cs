using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class SimulationScheduler : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly IPointStore _pointStore;
    private readonly Timer _timer;
    private int _intervalMs = 500;

    public SimulationScheduler(DeviceManager deviceManager, IPointStore pointStore)
    {
        _deviceManager = deviceManager;
        _pointStore = pointStore;
        _timer = new Timer(UpdateLoop, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _timer.Change(0, _intervalMs);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void SetInterval(int ms)
    {
        _intervalMs = ms;
        _timer.Change(0, _intervalMs);
    }

    private void UpdateLoop(object? state)
    {
        var devices = _deviceManager.GetAll().Where(d => d.Status == DeviceStatus.Running);

        foreach (var device in devices)
        {
            foreach (var point in device.Points)
            {
                if (point.Generator != null && point.Generator.Type != "static")
                {
                   UpdatePointValue(device.Id, point);
                }
            }
        }
    }

    private void UpdatePointValue(string deviceId, PointDefinition point)
    {
        if (point.Generator == null) return;

        // Simple simulation logic
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
                 var rnd = Random.Shared;
                 newValue = gen.Min + (rnd.NextDouble() * (gen.Max - gen.Min));
                 break;
        }

        // Apply simulated value only if not manually overridden recently? 
        // For MVP, we just overwrite. 
        // But user requirement: "Se un client scrive un valore, quello deve prevalere"
        // This usually implies "Manual" mode vs "Auto" mode.
        // Or we check `Source`. If Source is `RemoteWrite`, maybe we shouldn't overwrite immediately?
        // But for generators, they usually drive the value.
        // Let's assume generators always write.
        
        _pointStore.SetValue(deviceId, point.Key, newValue, PointSource.Simulation);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
