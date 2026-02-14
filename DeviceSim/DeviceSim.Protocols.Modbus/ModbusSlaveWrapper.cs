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
            // PROACTIVE VALIDATION to implement Exception Code 2 (Illegal Data Address)
            // We must check before _inner.ApplyRequest because NModbus swallows exceptions and returns Code 4.
            
            try
            {
                if (request is NModbus.Message.ReadHoldingInputRegistersRequest readReq)
                {
                    // Need to access StartAddress and NumberOfPoints.
                    // If these are not public, we might need a small helper, but usually they are.
                    // Try to cast DataStore to LinkedDataStore to access explicit PointSources
                    if (DataStore is LinkedDataStore linkedStore)
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
                }
                else if (request is NModbus.Message.WriteSingleRegisterRequestResponse writeSingle) 
                {
                    if (DataStore is LinkedDataStore linkedStore)
                    {
                        // WritePoints expects array. WriteSingleRegisterRequestResponse should have Data collection.
                        linkedStore.HoldingRegisters.WritePoints(writeSingle.StartAddress, writeSingle.Data.ToArray());
                    }
                }
                 else if (request is NModbus.Message.WriteMultipleRegistersRequest writeMulti) 
                {
                    if (DataStore is LinkedDataStore linkedStore)
                    {
                        linkedStore.HoldingRegisters.WritePoints(writeMulti.StartAddress, writeMulti.Data.ToArray());
                    }
                }
            }
            catch (IllegalDataAddressException)
            {
                // Return Exception Code 2
                return new CustomSlaveExceptionResponse(
                    request.TransactionId,
                    request.SlaveAddress,
                    request.FunctionCode,
                    2);
            }

            // Normal processing
            return _inner.ApplyRequest(request);
        }
    }
}
