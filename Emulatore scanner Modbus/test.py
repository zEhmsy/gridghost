from pymodbus.client import ModbusTcpClient as C
from pymodbus.exceptions import ModbusIOException

host='127.0.0.1'; port=1502
device_ids=[1,0,2,10]; addrs=[0,1]

c=C(host, port=port, timeout=2)
print('connect:', c.connect())

for dev in device_ids:
  for a in addrs:
    try:
      rH = c.read_holding_registers(a, count=1, device_id=dev)
      print(f'device_id={dev} addr={a} holding ->', ('OK '+str(rH.registers) if not rH.isError() else 'ERR '+str(rH)))
    except ModbusIOException as e:
      print(f'device_id={dev} addr={a} holding -> NO RESPONSE ({e})')
      break

    try:
      rI = c.read_input_registers(a, count=1, device_id=dev)
      print(f'device_id={dev} addr={a} input   ->', ('OK '+str(rI.registers) if not rI.isError() else 'ERR '+str(rI)))
    except ModbusIOException as e:
      print(f'device_id={dev} addr={a} input   -> NO RESPONSE ({e})')
      break

c.close()
