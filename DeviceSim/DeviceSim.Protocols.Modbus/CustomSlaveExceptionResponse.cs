using System;
using NModbus;

namespace DeviceSim.Protocols.Modbus
{
    public class CustomSlaveExceptionResponse : IModbusMessage
    {
        private readonly byte _exceptionCode;

        public CustomSlaveExceptionResponse(ushort transactionId, byte slaveAddress, byte functionCode, byte exceptionCode)
        {
            TransactionId = transactionId;
            SlaveAddress = slaveAddress;
            FunctionCode = (byte)(functionCode | 0x80);
            _exceptionCode = exceptionCode;
        }

        public ushort TransactionId { get; set; }
        public byte FunctionCode { get; set; }
        public byte SlaveAddress { get; set; }
        public byte[] MessageFrame => new byte[] { SlaveAddress, FunctionCode, _exceptionCode };
        public byte[] ProtocolDataUnit => new byte[] { FunctionCode, _exceptionCode };
        
        public void Initialize(byte[] frame) { }
    }
}
