using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using NModbus;
using NModbus.Data;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceSim.Protocols.Modbus;

public class ModbusAdapter : IProtocolAdapter
{
    public global::DeviceSim.Core.Models.ProtocolType Protocol => global::DeviceSim.Core.Models.ProtocolType.Modbus;

    private class SharedPortNetwork
    {
        public TcpListener Listener { get; }
        public IModbusSlaveNetwork SlaveNetwork { get; }
        public ConcurrentDictionary<byte, string> RunningSlaves { get; } = new();
        public CancellationTokenSource SharedCts { get; } = new();
        public Task ListenTask { get; }

        public SharedPortNetwork(TcpListener listener, IModbusSlaveNetwork slaveNetwork, Task listenTask)
        {
            Listener = listener;
            SlaveNetwork = slaveNetwork;
            ListenTask = listenTask;
        }
    }

    private static readonly ConcurrentDictionary<(string ip, int port), SharedPortNetwork> _sharedPortNetworks = new();
    private readonly ConcurrentDictionary<string, (string ip, int port, byte slaveId)> _deviceTracks = new();

    public async Task StartAsync(DeviceInstance instance, IPointStore pointStore, ILogSink log, CancellationToken ct)
    {
        var port = instance.Network.Port;
        var ipStr = instance.Network.BindIp;
        var ip = IPAddress.Parse(ipStr);
        var slaveId = instance.Network.DeviceAddress;
        if (slaveId == 0) slaveId = 1; // Fallback

        var key = (ipStr, port);
        SharedPortNetwork? sharedNet = null;

        lock (_sharedPortNetworks)
        {
            if (!_sharedPortNetworks.TryGetValue(key, out sharedNet))
            {
                var listener = new TcpListener(ip, port);
                listener.Start();
                log.Log("Info", $"Modbus TCP Listener started on {ipStr}:{port}", instance.Id);

                var factory = new ModbusFactory();
                var slaveNetwork = factory.CreateSlaveNetwork(listener);
                
                var sharedCts = new CancellationTokenSource();
                var listenTask = Task.Run(async () =>
                {
                    try
                    {
                        await slaveNetwork.ListenAsync(sharedCts.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        log.LogException(ex, $"Modbus shared listener error on {ipStr}:{port}", "System");
                    }
                    finally
                    {
                        listener.Stop();
                    }
                });

                sharedNet = new SharedPortNetwork(listener, slaveNetwork, listenTask);
                _sharedPortNetworks[key] = sharedNet;
            }
        }

        if (sharedNet.RunningSlaves.TryAdd(slaveId, instance.Id))
        {
            _deviceTracks[instance.Id] = (ipStr, port, slaveId);

            var linkedStore = new LinkedDataStore(pointStore, instance);
            var slave = new ModbusFactory().CreateSlave(slaveId, linkedStore);
            var wrappedSlave = new ModbusSlaveWrapper(slave);
            
            sharedNet.SlaveNetwork.AddSlave(wrappedSlave);
            log.Log("Info", $"Modbus Slave {slaveId} added to shared listener on {ipStr}:{port}", instance.Id);

            try
            {
                // Await device cancellation token
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Remove slave on stop
                sharedNet.SlaveNetwork.RemoveSlave(slaveId);
                sharedNet.RunningSlaves.TryRemove(slaveId, out _);
                _deviceTracks.TryRemove(instance.Id, out _);
                log.Log("Info", $"Modbus Slave {slaveId} removed from shared listener on {ipStr}:{port}", instance.Id);

                // Stop listener if no more active slaves
                lock (_sharedPortNetworks)
                {
                    if (sharedNet.RunningSlaves.IsEmpty)
                    {
                        if (_sharedPortNetworks.TryRemove(key, out _))
                        {
                            sharedNet.SharedCts.Cancel();
                            sharedNet.SharedCts.Dispose();
                            log.Log("Info", $"Modbus TCP Listener stopped on {ipStr}:{port}", instance.Id);
                        }
                    }
                }
            }
        }
        else
        {
            var msg = $"Modbus Slave ID {slaveId} is already running on {ipStr}:{port}";
            log.Log("Error", msg, instance.Id);
            throw new InvalidOperationException(msg);
        }
    }

    public Task StopAsync(DeviceInstance instance, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public bool IsRunning(DeviceInstance instance) => _deviceTracks.ContainsKey(instance.Id);
}
