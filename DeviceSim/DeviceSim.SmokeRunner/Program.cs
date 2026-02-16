using System;
using System.Net.Sockets;
using System.Threading;
using NModbus;

namespace DeviceSim.SmokeRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 502;
            int slaveId = 1;

            if (args.Length > 0) host = args[0];
            if (args.Length > 1) int.TryParse(args[1], out port);

            Console.WriteLine($"--- GridGhost C# Smoke Runner ---");
            Console.WriteLine($"Target: {host}:{port}, Slave: {slaveId}");

            bool allPassed = true;

            // 0. TCP Connect
            if (!TcpCheck(host, port))
            {
                Console.WriteLine("[FAIL] Cannot open TCP connection.");
                return 1;
            }
            Console.WriteLine("[PASS] TCP Connection Open");

            using (var client = new TcpClient(host, port))
            {
                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);
                master.Transport.ReadTimeout = 2000;
                master.Transport.WriteTimeout = 2000;

                // 1. Coil R/W (Offset 101 -> Coil 00102)
                allPassed &= TestCoil(master, (byte)slaveId, 101);

                // 2. Coil Block Scan (Range 96-120) - Sparse Tolerant
                allPassed &= TestCoilScan(master, (byte)slaveId, 96, 24);

                // 3. Holding Register R/W (Offset 10)
                allPassed &= TestHoldingRegister(master, (byte)slaveId, 10);

                // 4. Unmapped Register -> Exception Code 2
                allPassed &= TestUnmappedException(master, (byte)slaveId, 9999);
            }

            // 5. Lifecycle/Port Check (Optional arg)
            if (args.Length > 2 && args[2] == "--check-port-closed")
            {
                Console.WriteLine("\nChecking if port is closed...");
                if (!TcpCheck(host, port))
                {
                    Console.WriteLine("[PASS] Port is closed as expected.");
                }
                else
                {
                    Console.WriteLine("[FAIL] Port is still OPEN!");
                    allPassed = false;
                }
            }

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
    }
}
