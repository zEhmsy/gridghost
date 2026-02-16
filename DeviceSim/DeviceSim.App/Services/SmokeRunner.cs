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
            byte slaveId = 1;

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
            await master.WriteSingleRegisterAsync(slaveId, 0, 1234);
            var regVal = _pointStore.GetValue(deviceId, "Reg1");
            if (Convert.ToDouble(regVal.Value) != 1234) throw new Exception("Register write failed verification in PointStore.");

            // ---------------------------------------------------------
            // 7. Modbus Perfection Tests (FC05, FC0F, FC10, Code 02, Code 03)
            // ---------------------------------------------------------
            _logger.Log("Info", "7. Running Modbus Perfection Tests...", "System");

            // Test 7a: Write Single Coil (FC05) to unmapped address
            try
            {
                _logger.Log("Info", "   Testing FC05 (Write Single Coil) to unmapped address 100...", "System");
                await master.WriteSingleCoilAsync(slaveId, 100, true);
                throw new Exception("   -> FAILED: Expected SlaveException (Code 2) for unmapped FC05, got Success.");
            }
            catch (SlaveException se) when (se.SlaveExceptionCode == 2)
            {
                _logger.Log("Info", "   -> Success: Got Exception Code 2 (Illegal Data Address) for FC05.", "System");
            }
            catch (Exception ex)
            {
                throw new Exception($"   -> FAILED: Expected SlaveException Code 2 for FC05, got {ex.GetType().Name}: {ex.Message}");
            }

            // Test 7b: Write Multiple Coils (FC0F) to unmapped address
            try
            {
                _logger.Log("Info", "   Testing FC0F (Write Multiple Coils) to unmapped address 101...", "System");
                await master.WriteMultipleCoilsAsync(slaveId, 101, new bool[] { false });
                throw new Exception("   -> FAILED: Expected SlaveException (Code 2) for unmapped FC0F, got Success.");
            }
            catch (SlaveException se) when (se.SlaveExceptionCode == 2)
            {
                _logger.Log("Info", "   -> Success: Got Exception Code 2 (Illegal Data Address) for FC0F.", "System");
            }
            catch (Exception ex)
            {
                throw new Exception($"   -> FAILED: Expected SlaveException Code 2 for FC0F, got {ex.GetType().Name}: {ex.Message}");
            }

            // Test 7c: Write Single Register (FC06) to Unmapped Address (Expecting Code 02)
            try
            {
                _logger.Log("Info", "   Testing Write Single Register (FC06) to Unmapped Address (9999)...", "System");
                await master.WriteSingleRegisterAsync(slaveId, 9999, 123);
                throw new Exception("   -> FAILED: Expected SlaveException (Code 2) for unmapped FC06, got Success.");
            }
            catch (SlaveException se) when (se.SlaveExceptionCode == 2)
            {
                 _logger.Log("Info", "   -> Success: Got Exception Code 2 (Illegal Data Address) for FC06.", "System");
            }
            catch (Exception ex)
            {
                throw new Exception($"   -> FAILED: Expected SlaveException Code 2 for FC06, got {ex.GetType().Name}: {ex.Message}");
            }

            // Test 7d: Write Multiple Registers (FC10) to Unmapped Address (Expecting Code 02)
            try
            {
                _logger.Log("Info", "   Testing Write Multiple Registers (FC10) to Unmapped Address (9999)...", "System");
                await master.WriteMultipleRegistersAsync(slaveId, 9999, new ushort[] { 1, 2 });
                throw new Exception("   -> FAILED: Expected SlaveException (Code 2) for unmapped FC10, got Success.");
            }
            catch (SlaveException se) when (se.SlaveExceptionCode == 2)
            {
                 _logger.Log("Info", "   -> Success: Got Exception Code 2 (Illegal Data Address) for FC10.", "System");
            }
            catch (Exception ex)
            {
                throw new Exception($"   -> FAILED: Expected SlaveException Code 2 for FC10, got {ex.GetType().Name}: {ex.Message}");
            }

            // Test 7e: Read Holding Registers (FC03) from Unmapped Address (Expecting Code 02)
            try
            {
                _logger.Log("Info", "   Testing Read Holding Registers (FC03) from Unmapped Address (9999)...", "System");
                await master.ReadHoldingRegistersAsync(slaveId, 9999, 1);
                throw new Exception("   -> FAILED: Expected SlaveException (Code 2) for unmapped FC03, got Success.");
            }
            catch (SlaveException se) when (se.SlaveExceptionCode == 2)
            {
                _logger.Log("Info", "   -> Success: Got Exception Code 2 (Illegal Data Address) for FC03.", "System");
            }
            catch (Exception ex)
            {
                throw new Exception($"   -> FAILED: Expected SlaveException Code 2 for FC03, got {ex.GetType().Name}: {ex.Message}");
            }

            // Note: We cannot easily test Code 03 (Illegal Data Value) or Code 04 (Slave Device Failure)
            // without a specific template setup that defines such conditions.
            // For now, we verified unmapped addresses and basic writes/reads.
            
            _logger.Log("Info", "All Smoke Tests Passed!", "System");
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
