# GridGhost Smoke Tests

This directory contains diagnostic scripts to verify core GridGhost functionality, especially features critical for Niagara integration.

## Requirements

- Python 3.x
- `pymodbus` library

Install dependencies:
```bash
pip install pymodbus
```

## Running the Smoke Test

1. Start **GridGhost** and ensure the default device (or a mapped device) is running on port 502.
2. Run the script:
```bash
python smoke_test.py 127.0.0.1 502
```

### What it tests:
1. **Coil Write/Read**: Verifies that writing to a coil updates the store and reads back correctly.
2. **Holding Register Write/Read**: Verifies numeric register updates.
3. **Exception Code 2**: Attempts to read an unmapped register address. GridGhost should return **Exception Code 2 (Illegal Data Address)**. If it returns Code 4 or timeouts, the test fails.

## Lifecycle Test (Manual)

To verify that the **Enable/Disable** toggle correctly starts and stops the listener:

1. In GridGhost, **uncheck** the "Enable" box for the device.
2. Run the smoke test:
```bash
python smoke_test.py 127.0.0.1 502
```
3. The script should fail with `Could not connect`. If it still connects, the bug is regressed.
