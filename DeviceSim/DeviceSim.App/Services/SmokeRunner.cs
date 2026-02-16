using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services; // For DeviceManager
using NModbus;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeviceSim.App.Services;

public class SmokeRunner
{
    private readonly DeviceManager _deviceManager;
    private readonly ILogSink _logger;
    private readonly IPointStore _pointStore;

    public SmokeRunner(DeviceManager deviceManager, ILogSink logger, IPointStore pointStore)
    {
        _deviceManager = deviceManager;
        _logger = logger;
        _pointStore = pointStore;
    }

    public async Task RunSelfTestAsync(int port = 5502)
    {
        _logger.Log("Info", "Starting Smoke Test...", "System");

        // 1. Create a Test Device
        var deviceId = "smoke-test-device";
        // Cleanup if exists
        await _deviceManager.RemoveInstanceAsync(deviceId);

        var device = new DeviceInstance
        {
            Id = deviceId,
            Name = "Smoke Test Device",
            Protocol = DeviceSim.Core.Models.ProtocolType.Modbus,
            Network = new NetworkConfig { Port = port, BindIp = "127.0.0.1" },
            Points = new List<PointDefinition>
            {
                new PointDefinition { Key = "Coil1", Type = "bool", Modbus = new ModbusPointConfig { Kind = "Coil", Address = 0 } },
                new PointDefinition { Key = "Reg1", Type = "uint16", Modbus = new ModbusPointConfig { Kind = "Holding", Address = 0 } }
            }
        };

        try
        {
            _deviceManager.AddInstance(device);
            _logger.Log("Info", "Test Device Added.", "System");

            // 2. Start Device
            await _deviceManager.StartDeviceAsync(deviceId);
            
            // Wait for start
            // Since StartDeviceAsync awaits adapter.StartAsync, it should be running.
            // But we use task.run in manager, so wait a bit.
            await Task.Delay(1000); 

            if (device.State != DeviceInstance.DeviceState.Running)
            {
                throw new Exception($"Device failed to start. State: {device.State}, Error: {device.LastError}");
            }
            _logger.Log("Info", "Test Device Running.", "System");

            // 3. Connect Client and Verify
            using var client = new TcpClient("127.0.0.1", port);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            // Write Coil
            _logger.Log("Info", "Writing Coil 0 -> true", "System");
            await master.WriteSingleCoilAsync(1, 0, true);
            var coilVal = _pointStore.GetValue(deviceId, "Coil1");
            if (!Convert.ToBoolean(coilVal.Value)) throw new Exception("Coil write failed verification in PointStore.");

            // Read Coil
            var coils = await master.ReadCoilsAsync(1, 0, 1);
            if (!coils[0]) throw new Exception("Coil read failed.");

            // Write Register
            _logger.Log("Info", "Writing Register 0 -> 1234", "System");
            await master.WriteSingleRegisterAsync(1, 0, 1234);
            var regVal = _pointStore.GetValue(deviceId, "Reg1");
            if (Convert.ToDouble(regVal.Value) != 1234) throw new Exception("Register write failed verification in PointStore.");

            // Verify Unmapped Address (Exception Code 2)
            try
            {
                // Reading unmapped address 100
                await master.ReadHoldingRegistersAsync(1, 100, 1);
                throw new Exception("Read unmapped register SHOULD have failed.");
            }
            catch (SlaveException se)
            {
                if (se.SlaveExceptionCode != 2) throw new Exception($"Expected Exception Code 2, got {se.SlaveExceptionCode}");
                _logger.Log("Info", "Unmapped Address correctly returned Exception Code 2.", "System");
            }

            _logger.Log("Info", "Smoke Test Passed!", "System");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Smoke Test Failed", "System");
            throw; // Re-throw to signal failure if caller handles it
        }
        finally
        {
            await _deviceManager.RemoveInstanceAsync(deviceId);
        }
    }
}
