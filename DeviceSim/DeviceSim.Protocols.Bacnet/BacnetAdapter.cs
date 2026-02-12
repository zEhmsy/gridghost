using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeviceSim.Protocols.Bacnet;

public class BacnetAdapter : IProtocolAdapter
{
    public DeviceSim.Core.Models.ProtocolType Protocol => DeviceSim.Core.Models.ProtocolType.Bacnet;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, UdpClient> _listeners = new();

    public Task StartAsync(DeviceInstance instance, IPointStore pointStore, ILogSink log, CancellationToken ct)
    {
        var port = instance.Network.Port;
        // BACnet usually binds to 0.0.0.0 or specific IP. UDP.
        
        try
        {
            var udpClient = new UdpClient(port);
            _listeners[instance.Id] = udpClient;
            log.Log("Info", $"BACnet/IP 'Server' started on port {port} (Stub)", instance.Id);

            // Listen loop (Stub)
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync(ct);
                        log.Log("Debug", $"BACnet Rx: {result.Buffer.Length} bytes from {result.RemoteEndPoint}", instance.Id);
                        
                        // Parse basic? 
                        // If it's a Who-Is (0x10 0x08 ...), we could reply I-Am.
                        // For now just log.
                        
                        // Also update pointStore with values to simulate "ReadProperty" working locally?
                        // No, pointStore has values.
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        log.LogException(ex, "BACnet error", instance.Id);
                    }
                }
            }, ct);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            log.LogException(ex, "BACnet start error", instance.Id);
            throw;
        }
    }

    public Task StopAsync(DeviceInstance instance, CancellationToken ct)
    {
        if (_listeners.TryRemove(instance.Id, out var client))
        {
            client.Close();
        }
        return Task.CompletedTask;
    }

    public bool IsRunning(DeviceInstance instance) => _listeners.ContainsKey(instance.Id);
}
