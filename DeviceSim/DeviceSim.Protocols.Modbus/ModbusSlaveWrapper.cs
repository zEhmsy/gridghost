using System;
using NModbus;

namespace DeviceSim.Protocols.Modbus
{
    public class IllegalDataAddressException : Exception
    {
        public IllegalDataAddressException() : base("Illegal Data Address") { }
    }

    public class ModbusSlaveWrapper : IModbusSlave
    {
        private readonly IModbusSlave _inner;

        public ModbusSlaveWrapper(IModbusSlave inner)
        {
            _inner = inner;
        }

        public byte UnitId => _inner.UnitId;

        public ISlaveDataStore DataStore 
        { 
            get => _inner.DataStore; 
        }

        public IModbusMessage ApplyRequest(IModbusMessage request)
        {
            // PROACTIVE VALIDATION to implement Exception Code 2 (Illegal Data Address) & Code 3 (Illegal Data Value)
            // We must check before _inner.ApplyRequest because NModbus swallows exceptions or acts generic.
            
            try
            {
                if (DataStore is LinkedDataStore linkedStore)
                {
                    // --- READ REQUESTS (Verify Mapping Only) ---
                    if (request is NModbus.Message.ReadHoldingInputRegistersRequest readReq)
                    {
                        if (readReq.FunctionCode == 3) // Holding Registers
                        {
                             // This call will throw IllegalDataAddressException if invalid
                             linkedStore.HoldingRegisters.ReadPoints(readReq.StartAddress, readReq.NumberOfPoints);
                        }
                        else if (readReq.FunctionCode == 4) // Input Registers
                        {
                             linkedStore.InputRegisters.ReadPoints(readReq.StartAddress, readReq.NumberOfPoints);
                        }
                    }
                    
                    // --- WRITE REQUESTS (Verify Mapping AND Access) ---
                    
                    // FC 06: Write Single Register
                    else if (request is NModbus.Message.WriteSingleRegisterRequestResponse writeSingle) 
                    {
                        // WritePoints expects array.
                        linkedStore.HoldingRegisters.WritePoints(writeSingle.StartAddress, new ushort[] { writeSingle.Data[0] });
                    }
                    
                    // FC 10: Write Multiple Registers
                    else if (request is NModbus.Message.WriteMultipleRegistersRequest writeMulti) 
                    {
                        var data = writeMulti.Data.Take(writeMulti.NumberOfPoints).ToArray();
                        linkedStore.HoldingRegisters.WritePoints(writeMulti.StartAddress, data);
                    }
                    
                    // FC 05: Write Single Coil
                    else if (request is NModbus.Message.WriteSingleCoilRequestResponse writeCoil)
                    {
                         // NModbus uses 0xFF00 for ON, 0x0000 for OFF
                        bool val = writeCoil.Data[0] == 0xFF00;
                        linkedStore.CoilDiscretes.WritePoints(writeCoil.StartAddress, new bool[] { val });
                    }
                    
                    // FC 15 (0x0F): Write Multiple Coils
                    else if (request is NModbus.Message.WriteMultipleCoilsRequest writeMultiCoils)
                    {
                         // DiscreteCollection ToArray returns bool[] which might be padded to byte boundary (8)
                         var data = writeMultiCoils.Data.Take(writeMultiCoils.NumberOfPoints).ToArray();
                         linkedStore.CoilDiscretes.WritePoints(writeMultiCoils.StartAddress, data);
                    }
                }
            }
            catch (IllegalDataAddressException)
            {
                return new CustomSlaveExceptionResponse(
                    request.TransactionId,
                    request.SlaveAddress,
                    request.FunctionCode,
                    2);
            }
            catch (IllegalDataValueException) 
            {
                // Thrown when trying to write to a Read-Only point
                return new CustomSlaveExceptionResponse(
                    request.TransactionId,
                    request.SlaveAddress,
                    request.FunctionCode,
                    3);
            }
            catch (Exception)
            {
                 // Fallback to Code 4 (Slave Device Failure) or let NModbus handle it
                 throw;
            }

            // Normal processing if no exceptions thrown during validation
            // Note: For writes, we technically already applied the write in the validation block above 
            // via linkedStore.X.WritePoints which commits to the store. 
            // However, NModbus *also* tries to write to its internal memory if we pass it through.
            // Since LinkedDataStore IS the storage, passing it to _inner.ApplyRequest might be redundant/safe
            // BUT _inner.ApplyRequest generates the correct RESPONSE message.
            return _inner.ApplyRequest(request);
        }
    }
    
    public class IllegalDataValueException : Exception
    {
        public IllegalDataValueException() : base("Illegal Data Value") { }
    }
}
