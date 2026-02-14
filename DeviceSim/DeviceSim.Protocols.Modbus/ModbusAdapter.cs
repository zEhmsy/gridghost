using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using NModbus;
using NModbus.Data;
using System.Net;
using System.Net.Sockets;

namespace DeviceSim.Protocols.Modbus;

public class ModbusAdapter : IProtocolAdapter
{
    public global::DeviceSim.Core.Models.ProtocolType Protocol => global::DeviceSim.Core.Models.ProtocolType.Modbus;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TcpListener> _listeners = new();

    public async Task StartAsync(DeviceInstance instance, IPointStore pointStore, ILogSink log, CancellationToken ct)
    {
        var port = instance.Network.Port;
        var ip = IPAddress.Parse(instance.Network.BindIp);
        var listener = new TcpListener(ip, port);
        
        try
        {
            listener.Start();
            log.Log("Info", $"Modbus TCP Server started on {ip}:{port}", instance.Id);

            var factory = new ModbusFactory();
            var slaveNetwork = factory.CreateSlaveNetwork(listener);

            // Use our custom LinkedDataStore which maps directly to PointStore
            var linkedStore = new LinkedDataStore(pointStore, instance);
            
            var slave = factory.CreateSlave(1, linkedStore);
            // Wrap slave to handle exceptions (return Code 2 for Illegal Address)
            var wrappedSlave = new ModbusSlaveWrapper(slave);
            slaveNetwork.AddSlave(wrappedSlave);

            // No need for sync loop anymore!
            
            await slaveNetwork.ListenAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            log.LogException(ex, "Modbus server error", instance.Id);
            throw;
        }
        finally
        {
            listener.Stop();
            _listeners.TryRemove(instance.Id, out _);
        }
    }

    public Task StopAsync(DeviceInstance instance, CancellationToken ct)
    {
        if (_listeners.TryGetValue(instance.Id, out var listener))
        {
            listener.Stop();
        }
        return Task.CompletedTask;
    }

    public bool IsRunning(DeviceInstance instance) => _listeners.ContainsKey(instance.Id);


}
