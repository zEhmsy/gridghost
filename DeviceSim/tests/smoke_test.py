import sys
import time
from pymodbus.client import ModbusTcpClient
from pymodbus.exceptions import ModbusException
from pymodbus.pdu import ExceptionResponse

def run_smoke_test(host='127.0.0.1', port=502, slave=1):
    print(f"--- Starting GridGhost Smoke Test on {host}:{port} (Slave {slave}) ---")
    client = ModbusTcpClient(host, port=port)
    
    if not client.connect():
        print(f"FAILED: Could not connect to {host}:{port}. Is GridGhost running?")
        sys.exit(1)

    try:
        # 1. Coil Test (00102)
        print("\n[TEST 1] Coil Write/Read (Coil 102)...")
        # Coil 102 is offset 101
        addr = 101
        val_to_write = True
        print(f"Writing {val_to_write} to Coil {addr+1}...")
        client.write_coil(addr, val_to_write, slave=slave)
        
        time.sleep(0.5)
        
        rr = client.read_coils(addr, count=1, slave=slave)
        if rr.isError():
            print(f"FAILED: Read error: {rr}")
        else:
            print(f"Read value: {rr.bits[0]}")
            if rr.bits[0] == val_to_write:
                print("SUCCESS: Coil value matches.")
            else:
                print("FAILED: Coil value mismatch.")

        # 2. Holding Register Test (e.g. 40001 -> offset 0)
        print("\n[TEST 2] Holding Register Write/Read (40001)...")
        addr = 0
        val_to_write = 1234
        print(f"Writing {val_to_write} to Register {addr+40001}...")
        client.write_register(addr, val_to_write, slave=slave)
        
        time.sleep(0.5)
        
        rr = client.read_holding_registers(addr, count=1, slave=slave)
        if rr.isError():
            print(f"FAILED: Read error: {rr}")
        else:
            print(f"Read value: {rr.registers[0]}")
            if rr.registers[0] == val_to_write:
                print("SUCCESS: Register value matches.")
            else:
                print("FAILED: Register value mismatch.")

        # 3. Exception Code 2 Test (Unmapped)
        # Try reading offset 9999 (likely unmapped)
        print("\n[TEST 3] Unmapped Register -> Exception Code 2...")
        unmapped_addr = 9999
        rr = client.read_holding_registers(unmapped_addr, count=1, slave=slave)
        if isinstance(rr, ExceptionResponse):
            print(f"Received Exception Response: Code {rr.exception_code}")
            if rr.exception_code == 2:
                print("SUCCESS: Correctly received Exception Code 2 (Illegal Data Address).")
            else:
                print(f"FAILED: Received Code {rr.exception_code}, expected 2.")
        else:
            print("FAILED: Expected ExceptionResponse, got success or different error.")

        print("\n--- Smoke Test Completed Successfully (Local Server Portion) ---")
        print("NOTE: To test Lifecycle (Start/Stop), disable the device in GridGhost and run this script again.")
        print("It should fail to connect if Stop worked correctly.")

    except Exception as e:
        print(f"CRITICAL ERROR during test: {e}")
    finally:
        client.close()

if __name__ == "__main__":
    host = sys.argv[1] if len(sys.argv) > 1 else '127.0.0.1'
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 502
    run_smoke_test(host, port)
