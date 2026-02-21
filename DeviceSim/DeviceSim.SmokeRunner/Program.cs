using System;
using System.Net.Sockets;
using System.Threading;
using NModbus;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for List

namespace DeviceSim.SmokeRunner
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 5502; // Use non-standard port for test
            int slaveId = 1;

            if (args.Length > 0) host = args[0];
            if (args.Length > 1) int.TryParse(args[1], out port);

            Console.WriteLine($"--- GridGhost C# Smoke Runner (Direct Adapter Mode) ---");
            Console.WriteLine($"Target: {host}:{port}, Slave: {slaveId}");

            // --- SETUP SERVER ---
            var pointStore = new DeviceSim.Core.Services.PointStore();
            var logger = new DeviceSim.Core.Services.LogService();
            var adapter = new DeviceSim.Protocols.Modbus.ModbusAdapter();

            // Create Device
            var device = new DeviceSim.Core.Models.DeviceInstance
            {
                Id = "smoke-test",
                Name = "Smoke Test Device",
                Enabled = true,
                Protocol = DeviceSim.Core.Models.ProtocolType.Modbus,
                Network = new DeviceSim.Core.Models.NetworkConfig { Port = port, BindIp = "127.0.0.1" },
                Points = new List<DeviceSim.Core.Models.PointDefinition>
                {
                    // Coil 101 (addr 100) -> for FC05
                    new DeviceSim.Core.Models.PointDefinition { 
                        Key = "Coil101", 
                        Name = "Coil 101", 
                        Type = "bool", 
                        Access = DeviceSim.Core.Models.AccessMode.ReadWrite,
                        Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind = "Coil", Address = 100 } 
                    },
                    // Coil 96, 97, 98 -> for FC0F (3 coils)
                   new DeviceSim.Core.Models.PointDefinition { Key = "C96", Type="bool", Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Coil", Address=96 } },
                   new DeviceSim.Core.Models.PointDefinition { Key = "C97", Type="bool", Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Coil", Address=97 } },
                   new DeviceSim.Core.Models.PointDefinition { Key = "C98", Type="bool", Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Coil", Address=98 } },

                   // Holding 10 -> for FC10
                   new DeviceSim.Core.Models.PointDefinition { Key = "H10", Type="uint16", Access=DeviceSim.Core.Models.AccessMode.ReadWrite, Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Holding", Address=10 } },
                   new DeviceSim.Core.Models.PointDefinition { Key = "H11", Type="uint16", Access=DeviceSim.Core.Models.AccessMode.ReadWrite, Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Holding", Address=11 } },
                   
                   // Holding 20 -> Read Only
                   new DeviceSim.Core.Models.PointDefinition { Key = "RO20", Type="uint16", Access=DeviceSim.Core.Models.AccessMode.Read, Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Holding", Address=20 } },
                   
                   // Holding 30 -> HoldForSeconds
                   new DeviceSim.Core.Models.PointDefinition { 
                       Key = "H30", 
                       Type="uint16", 
                       Access=DeviceSim.Core.Models.AccessMode.ReadWrite, 
                       OverrideMode = DeviceSim.Core.Models.ExternalWriteOverrideMode.HoldForSeconds,
                       OverrideDurationSeconds = 2,
                       Generator = new DeviceSim.Core.Models.GeneratorConfig { Type = "random" },
                       Modbus = new DeviceSim.Core.Models.ModbusPointConfig { Kind="Holding", Address=30 } 
                   },
                }
            };
            
            // Initialize Points in Store (Required for SetValue to work!)
            pointStore.InitializePoints(device.Id, device.Points);

            // Start Adapter in Background
            var cts = new CancellationTokenSource();
            var serverTask = Task.Run(async () => 
            {
                try {
                    await adapter.StartAsync(device, pointStore, logger, cts.Token);
                } catch (OperationCanceledException) {}
                  catch (Exception ex) { Console.WriteLine($"Server Error: {ex}"); }
            });
            
            Console.WriteLine("Server Starting...");
            // Wait for port to open
            bool connected = false;
            for(int i=0; i<10; i++) 
            {
                if (TcpCheck(host, port)) { connected = true; break; }
                await Task.Delay(500);
            }

            if (!connected)
            {
                Console.WriteLine("[FAIL] Server failed to start or open port.");
                cts.Cancel();
                return 1;
            }
            
            Console.WriteLine("[PASS] Server Listening");

            bool allPassed = true;

            using (var client = new TcpClient(host, port))
            {
                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);
                master.Transport.ReadTimeout = 2000;
                master.Transport.WriteTimeout = 2000;

                // 1. FC05
                allPassed &= TestCoilFC05(master, (byte)slaveId, 100);

                // 2. FC0F
                allPassed &= TestCoilFC0F(master, (byte)slaveId, 96);

                // 3. FC10
                allPassed &= TestWriteMultipleRegisters(master, (byte)slaveId, 10);

                // 4. Unmapped Write (Code 2) - Address 9999 is definitely unmapped
                allPassed &= TestUnmappedWriteException(master, (byte)slaveId, 9999);
                
                // 5. Read-Only Write (Code 3) - Address 20 is ReadOnly
                allPassed &= TestReadOnlyWriteException(master, (byte)slaveId, 20);

                // 6. HoldForSeconds Override
                allPassed &= await TestHoldForSecondsOverride(master, (byte)slaveId, 30, device.Points.Find(p => p.Key == "H30"));

                // 7. Store Type Guard Enforcement
                allPassed &= TestStoreTypeEnforcement(pointStore, device.Id);
            }
            
            cts.Cancel();
            try { await serverTask; } catch {}
            
            Console.WriteLine("\n--- Test Summary ---");
            Console.WriteLine(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED");
            return allPassed ? 0 : 1;
        }

        static bool TcpCheck(string host, int port)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var result = c.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    if (!success) return false;
                    c.EndConnect(result);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TestCoil(IModbusMaster master, byte slaveId, ushort addr)
        {
            try
            {
                Console.Write($"[TEST] Coil R/W @ {addr}: ");
                master.WriteSingleCoil(slaveId, addr, true);
                Thread.Sleep(50);
                var res = master.ReadCoils(slaveId, addr, 1);
                if (res[0] != true) throw new Exception("Readback failed (expected True)");

                master.WriteSingleCoil(slaveId, addr, false);
                Thread.Sleep(50);
                res = master.ReadCoils(slaveId, addr, 1);
                if (res[0] != false) throw new Exception("Readback failed (expected False)");

                Console.WriteLine("PASS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ({ex.Message})");
                return false;
            }
        }

        static bool TestCoilScan(IModbusMaster master, byte slaveId, ushort start, ushort count)
        {
            try
            {
                Console.Write($"[TEST] Coil Block Scan @ {start} (len {count}): ");
                var res = master.ReadCoils(slaveId, start, count);
                if (res.Length != count) throw new Exception($"Length mismatch (got {res.Length})");
                Console.WriteLine("PASS (Sparse tolerant)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ({ex.Message})");
                return false;
            }
        }

        static bool TestHoldingRegister(IModbusMaster master, byte slaveId, ushort addr)
        {
            try
            {
                Console.Write($"[TEST] Holding R/W @ {addr}: ");
                ushort val = 12345;
                master.WriteSingleRegister(slaveId, addr, val);
                Thread.Sleep(50);
                var res = master.ReadHoldingRegisters(slaveId, addr, 1);
                if (res[0] != val) throw new Exception($"Readback failed (got {res[0]})");
                Console.WriteLine("PASS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ({ex.Message})");
                return false;
            }
        }

        static bool TestUnmappedException(IModbusMaster master, byte slaveId, ushort addr)
        {
            try
            {
                Console.Write($"[TEST] Unmapped Read @ {addr} (Expect Code 2): ");
                master.ReadHoldingRegisters(slaveId, addr, 1);
                Console.WriteLine("FAIL (No exception thrown)");
                return false;
            }
            catch (SlaveException ex)
            {
                if (ex.SlaveExceptionCode == 2)
                {
                    Console.WriteLine("PASS (Caught Code 2)");
                    return true;
                }
                Console.WriteLine($"FAIL (Wrong Code: {ex.SlaveExceptionCode})");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (Unexpected Error: {ex.GetType().Name} - {ex.Message})");
                return false;
            }
        }
        static bool TestCoilFC05(IModbusMaster master, byte slaveId, ushort addr)
        {
             try
            {
                Console.Write($"[TEST] FC05 Write Single Coil @ {addr}: ");
                master.WriteSingleCoil(slaveId, addr, true);
                var res = master.ReadCoils(slaveId, addr, 1);
                if (res[0] != true) throw new Exception("Readback failed");
                master.WriteSingleCoil(slaveId, addr, false);
                Console.WriteLine("PASS");
                return true;
            }
             catch (Exception ex) { Console.WriteLine($"FAIL ({ex.Message})"); return false; }
        }

        static bool TestCoilFC0F(IModbusMaster master, byte slaveId, ushort addr)
        {
             try
            {
                Console.Write($"[TEST] FC0F Write Multiple Coils @ {addr}: ");
                master.WriteMultipleCoils(slaveId, addr, new bool[] { true, false, true });
                var res = master.ReadCoils(slaveId, addr, 3);
                if (res[0] != true || res[1] != false || res[2] != true) throw new Exception("Readback failed");
                Console.WriteLine("PASS");
                return true;
            }
             catch (Exception ex) { Console.WriteLine($"FAIL ({ex.Message})"); return false; }
        }

        static bool TestWriteMultipleRegisters(IModbusMaster master, byte slaveId, ushort addr)
        {
             try
            {
                Console.Write($"[TEST] FC10 Write Multiple Registers @ {addr}: ");
                master.WriteMultipleRegisters(slaveId, addr, new ushort[] { 123, 456 });
                var res = master.ReadHoldingRegisters(slaveId, addr, 2);
                if (res[0] != 123 || res[1] != 456) throw new Exception("Readback failed");
                Console.WriteLine("PASS");
                return true;
            }
             catch (Exception ex) { Console.WriteLine($"FAIL ({ex.Message})"); return false; }
        }

        static bool TestUnmappedWriteException(IModbusMaster master, byte slaveId, ushort addr)
        {
            try
            {
                Console.Write($"[TEST] Unmapped Write (FC06/10) @ {addr} (Expect Code 2): ");
                master.WriteSingleRegister(slaveId, addr, 999);
                Console.WriteLine("FAIL (No exception default)");
                return false;
            }
            catch (SlaveException ex)
            {
                if (ex.SlaveExceptionCode == 2) { Console.WriteLine("PASS (Caught Code 2)"); return true; }
                Console.WriteLine($"FAIL (Wrong Code: {ex.SlaveExceptionCode})");
                return false;
            }
             catch (Exception ex) { Console.WriteLine($"FAIL ({ex.GetType().Name})"); return false; }
        }

        static bool TestReadOnlyWriteException(IModbusMaster master, byte slaveId, ushort addr)
        {
            try
            {
                Console.Write($"[TEST] Read-Only Write (FC06) @ {addr} (Expect Code 3): ");
                master.WriteSingleRegister(slaveId, addr, 999);
                Console.WriteLine("FAIL (No exception default)");
                return false;
            }
            catch (SlaveException ex)
            {
                if (ex.SlaveExceptionCode == 3) { Console.WriteLine("PASS (Caught Code 3: Illegal Data Value)"); return true; }
                Console.WriteLine($"FAIL (Wrong Code: {ex.SlaveExceptionCode})");
                return false;
            }
             catch (Exception ex) { Console.WriteLine($"FAIL ({ex.GetType().Name})"); return false; }
        }

        static async Task<bool> TestHoldForSecondsOverride(IModbusMaster master, byte slaveId, ushort addr, DeviceSim.Core.Models.PointDefinition point)
        {
            try
            {
                Console.Write($"[TEST] HoldForSeconds Override @ {addr}: ");
                
                if (point.Generator?.Type != "random") throw new Exception("Expected random generator initially");
                
                master.WriteSingleRegister(slaveId, addr, 555);
                await Task.Delay(100);
                
                if (point.Generator?.Type != "static") throw new Exception("Expected generator to be static during hold");
                var res = master.ReadHoldingRegisters(slaveId, addr, 1);
                if (res[0] != 555) throw new Exception("Readback failed");
                
                await Task.Delay(2100); // Wait for timer (2s)
                
                if (point.Generator?.Type != "random") throw new Exception("Expected generator to return to random after hold");
                
                Console.WriteLine("PASS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ({ex.Message})");
                return false;
            }
        }

        static bool TestStoreTypeEnforcement(DeviceSim.Core.Interfaces.IPointStore store, string deviceId)
        {
            try
            {
                Console.Write("[TEST] Direct UI/Store Type Guard Enforcement: ");
                
                // Intentionally attempt to assign a bool directly to a numeric point,
                // simulating Avalonia DataGrid virtualization corrupting the binding.
                store.SetValue(deviceId, "H10", true, DeviceSim.Core.Models.PointSource.Manual);
                
                var pt = store.GetValue(deviceId, "H10");
                if (pt.Value is bool) 
                {
                    throw new Exception("Guard failed! Store allowed boolean assignment to numeric point H10.");
                }
                
                Console.WriteLine("PASS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ({ex.Message})");
                return false;
            }
        }
    }
}
