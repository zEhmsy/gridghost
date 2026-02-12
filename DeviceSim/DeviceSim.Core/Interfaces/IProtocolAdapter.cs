using DeviceSim.Core.Models;

namespace DeviceSim.Core.Interfaces;

public interface IProtocolAdapter
{
    ProtocolType Protocol { get; }
    Task StartAsync(DeviceInstance instance, IPointStore pointStore, ILogSink log, CancellationToken ct);
    Task StopAsync(DeviceInstance instance, CancellationToken ct);
    bool IsRunning(DeviceInstance instance);
}
