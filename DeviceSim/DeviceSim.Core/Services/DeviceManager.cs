using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class DeviceManager
{
    private readonly IEnumerable<IProtocolAdapter> _adapters;
    private readonly IPointStore _pointStore;
    private readonly ILogSink _logger;
    private readonly SimulationScheduler _scheduler;
    private readonly ConfigurationService _configService;

    // deviceId -> Instance
    private readonly ConcurrentDictionary<string, DeviceInstance> _instances = new();

    // deviceId -> (Cts, Task)
    private readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, Task Task)> _runningDevices = new();

    // deviceId -> Semaphore to serialize Start/Stop/Edit
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks = new();

    public event Action<DeviceInstance>? OnDeviceUpdated;
    public event Action<string>? OnDeviceRemoved;

    public DeviceManager(IEnumerable<IProtocolAdapter> adapters, IPointStore pointStore, ILogSink logger, SimulationScheduler scheduler, ConfigurationService configService, TemplateRepository templateRepo)
    {
        _adapters = adapters;
        _pointStore = pointStore;
        _logger = logger;
        _scheduler = scheduler;
        _configService = configService;

        // Load persisted devices
        var saved = _configService.Load();
        if (saved != null && saved.Any())
        {
            foreach (var d in saved)
            {
                _instances[d.Id] = d;
                _pointStore.InitializePoints(d.Id, d.Points);
            }
        }
        else
        {
            // First run? Add default if nothing
            var templates = templateRepo.LoadAll();
            var tpl = templates.FirstOrDefault();
            if (tpl != null)
            {
                var def = DeviceInstance.FromTemplate(tpl);
                AddInstance(def); // This will save
            }
        }
    }

    public void AddInstance(DeviceInstance instance)
    {
        _instances[instance.Id] = instance;
        _pointStore.InitializePoints(instance.Id, instance.Points);
        _configService.Save(_instances.Values);
        OnDeviceUpdated?.Invoke(instance);
    }

    public async Task RemoveInstanceAsync(string id)
    {
        await StopDeviceAsync(id);
        
        _instances.TryRemove(id, out _);
        _pointStore.RemoveDevice(id);
        _deviceLocks.TryRemove(id, out _); // Clean up lock
        
        _configService.Save(_instances.Values);
        OnDeviceRemoved?.Invoke(id);
    }

    public IEnumerable<DeviceInstance> GetAll() => _instances.Values;

    public async Task StartDeviceAsync(string id)
    {
        if (!_instances.TryGetValue(id, out var instance)) return;

        var deviceLock = _deviceLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync();

        try
        {
            if (_runningDevices.ContainsKey(id)) return; // Already running

            // Check Port
            if (IsPortInUse(instance.Network.Port))
            {
                var msg = $"Port {instance.Network.Port} is already in use.";
                _logger.Log("Error", msg, id);
                instance.State = DeviceInstance.DeviceState.Faulted;
                instance.LastError = msg;
                instance.Enabled = false; // Force disable
                OnDeviceUpdated?.Invoke(instance);
                return;
            }

            // Linux Privileged Port Check
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) && 
                instance.Network.Port < 1024)
            {
                // We can't easily check for CAP_NET_BIND_SERVICE, so we warn if 502 is used and fails.
                // But better to fail fast or at least Log a warning?
                // The requirement says: "add a clear user-visible note/error when bind fails on Linux due to permissions (<1024)."
                // We'll let it try to start, and catch the specific socket exception below to give a better error message.
                // However, user asked to "just show an actionable error message".
                // We'll do it in the catch block for AccessDenied/SocketException.
            }

            var adapter = _adapters.FirstOrDefault(a => a.Protocol == instance.Protocol);
            if (adapter == null)
            {
                _logger.Log("Error", $"No adapter found for protocol {instance.Protocol}", id);
                instance.State = DeviceInstance.DeviceState.Faulted;
                instance.LastError = "No adapter found";
                instance.Enabled = false;
                OnDeviceUpdated?.Invoke(instance);
                return;
            }

            var cts = new CancellationTokenSource();
            
            instance.State = DeviceInstance.DeviceState.Starting;
            instance.LastError = null;
            instance.Enabled = true;
            OnDeviceUpdated?.Invoke(instance);

            _scheduler.StartDeviceSimulation(instance);

            var task = Task.Run(async () => 
            {
                try
                {
                    _logger.Log("Info", "Starting device protocol adapter...", id);
                    await adapter.StartAsync(instance, _pointStore, _logger, cts.Token);
                    
                    // If we get here, adapter started OK? 
                    // Usually StartAsync blocks for listeners, but if it returns immediately it might be problem.
                    // Assuming standard behavior of blocking or long-running. 
                    // But if it blocks, we can't set Running here efficiently unless we do it before await.
                    // Actually, for listeners, StartAsync usually blocks.
                    // So we should set Running BEFORE awaiting if we want UI to update immediately.
                    // However, if StartAsync throws immediately, we catch it.
                }
                catch (OperationCanceledException) 
                {
                    _logger.Log("Info", "Adapter stopped (canceled).", id);
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                    if (ex is System.Net.Sockets.SocketException sockEx && sockEx.SocketErrorCode == System.Net.Sockets.SocketError.AccessDenied) 
                    {
                         msg = $"Access Denied (Port {instance.Network.Port}). On Linux, ports < 1024 require root or 'setcap'. Try port 1502+.";
                    }
                    
                    _logger.LogException(ex, "Device adapter crashed", id);
                    instance.State = DeviceInstance.DeviceState.Faulted;
                    instance.LastError = msg;
                    instance.Enabled = false;
                    OnDeviceUpdated?.Invoke(instance);
                }
            }, cts.Token);

            _runningDevices[id] = (cts, task);
            
            // Assume running if no check throw
            instance.State = DeviceInstance.DeviceState.Running;
            OnDeviceUpdated?.Invoke(instance);
        }
        finally
        {
            deviceLock.Release();
        }
    }

    public async Task StopDeviceAsync(string id)
    {
        var deviceLock = _deviceLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync();

        try
        {
            if (_runningDevices.TryRemove(id, out var running))
            {
                var instance = _instances[id];
                _logger.Log("Info", "Stopping device...", id);

                instance.State = DeviceInstance.DeviceState.Stopping;
                OnDeviceUpdated?.Invoke(instance);
                
                // 1. Stop Simulation
                await _scheduler.StopDeviceSimulationAsync(id);

                // 2. Cancel adapter CT
                running.Cts.Cancel();
                try
                {
                    var adapter = _adapters.FirstOrDefault(a => a.Protocol == instance.Protocol);
                    if (adapter != null)
                    {
                        // Give it a moment to shut down gracefully
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await adapter.StopAsync(instance, timeoutCts.Token);
                    }
                    
                    // Wait for the Task to complete
                    await running.Task.WaitAsync(TimeSpan.FromSeconds(3));
                }
                catch (Exception ex)
                {
                     _logger.Log("Error", $"Error stopping device adapter: {ex.Message}", id);
                }
                finally
                {
                    running.Cts.Dispose();
                }

                if (_instances.TryGetValue(id, out var inst))
                {
                    inst.State = DeviceInstance.DeviceState.Stopped;
                    inst.Enabled = false;
                    inst.LastError = null;
                    OnDeviceUpdated?.Invoke(inst);
                    _logger.Log("Info", "Device stopped", id);
                }
            }
        }
        finally
        {
            deviceLock.Release();
        }
    }

    private bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
            return tcpConnInfoArray.Any(e => e.Port == port);
        }
        catch
        {
            return false; // Can't check, assume free? Or fail safe?
        }
    }
}
