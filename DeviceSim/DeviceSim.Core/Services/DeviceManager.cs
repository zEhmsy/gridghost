using System.Collections.Concurrent;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class DeviceManager
{
    private readonly IEnumerable<IProtocolAdapter> _adapters;
    private readonly IPointStore _pointStore;
    private readonly ILogSink _logger;

    // deviceId -> Instance
    private readonly ConcurrentDictionary<string, DeviceInstance> _instances = new();
    
    // deviceId -> (Cts, Task)
    private readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, Task Task)> _runningDevices = new();

    public event Action<DeviceInstance>? OnDeviceUpdated;
    public event Action<string>? OnDeviceRemoved;

    public DeviceManager(IEnumerable<IProtocolAdapter> adapters, IPointStore pointStore, ILogSink logger)
    {
        _adapters = adapters;
        _pointStore = pointStore;
        _logger = logger;
    }

    public void AddInstance(DeviceInstance instance)
    {
        _instances[instance.Id] = instance;
        _pointStore.InitializePoints(instance.Id, instance.Points);
        OnDeviceUpdated?.Invoke(instance);
    }

    public async Task RemoveInstanceAsync(string id)
    {
        await StopDeviceAsync(id);
        _instances.TryRemove(id, out _);
        _pointStore.RemoveDevice(id);
        OnDeviceRemoved?.Invoke(id);
    }

    public IEnumerable<DeviceInstance> GetAll() => _instances.Values;

    public async Task StartDeviceAsync(string id)
    {
        if (!_instances.TryGetValue(id, out var instance)) return;
        if (_runningDevices.ContainsKey(id)) return; // Already running

        var adapter = _adapters.FirstOrDefault(a => a.Protocol == instance.Protocol);
        if (adapter == null)
        {
            _logger.Log("Error", $"No adapter found for protocol {instance.Protocol}", id);
            instance.LastError = "No adapter found";
            instance.Status = DeviceStatus.Error;
            OnDeviceUpdated?.Invoke(instance);
            return;
        }

        var cts = new CancellationTokenSource();
        instance.Status = DeviceStatus.Running;
        instance.Enabled = true;
        instance.LastError = null;
        OnDeviceUpdated?.Invoke(instance);

        var task = Task.Run(async () => 
        {
            try
            {
                _logger.Log("Info", "Starting device...", id);
                await adapter.StartAsync(instance, _pointStore, _logger, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Device crashed", id);
                instance.Status = DeviceStatus.Error;
                instance.LastError = ex.Message;
                OnDeviceUpdated?.Invoke(instance);
            }
        }, cts.Token);

        _runningDevices[id] = (cts, task);
        await Task.CompletedTask;
    }

    public async Task StopDeviceAsync(string id)
    {
        if (_runningDevices.TryRemove(id, out var running))
        {
            var instance = _instances[id];
            _logger.Log("Info", "Stopping device...", id);
            
            running.Cts.Cancel();
            try
            {
                // We also need to call StopAsync on adapter if it has cleanup logic that doesn't rely solely on CT
                var adapter = _adapters.FirstOrDefault(a => a.Protocol == instance.Protocol);
                if (adapter != null)
                {
                    await adapter.StopAsync(instance, CancellationToken.None);
                }
                
                await running.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                 _logger.Log("Error", $"Error stopping device: {ex.Message}", id);
            }
            finally
            {
                running.Cts.Dispose();
            }

            if (_instances.TryGetValue(id, out var inst))
            {
                inst.Status = DeviceStatus.Stopped;
                inst.Enabled = false;
                OnDeviceUpdated?.Invoke(inst);
                _logger.Log("Info", "Device stopped", id);
            }
        }
    }
}
