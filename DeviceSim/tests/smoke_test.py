#!/usr/bin/env python3
"""
GridGhost smoke test (Modbus TCP) — PASS/FAIL with exit code.

Requirements:
  pip install pymodbus

Test coverage:
  1) TCP connect
  2) Coil write/read (default: 00102 -> offset 101)
  3) Coils & Discretes block scan (sparse tolerant)
  4) Holding write/read on mapped offsets (default: 10 and 12)
  5) Unmapped register read/write must return Modbus exception code 2 (Illegal Data Address)

Optional:
  --expect-stopped : just checks the TCP port is closed (useful after you disable device in UI)

Exit codes:
  0 = all tests passed
  1 = one or more tests failed
"""

import argparse
import socket
import sys
import time
from dataclasses import dataclass
from typing import Optional, Sequence

from pymodbus.client import ModbusTcpClient


# ---------- helpers ----------

def tcp_open(host: str, port: int, timeout: float = 0.8) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except OSError:
        return False


def fmt_ok(ok: bool) -> str:
    return "PASS" if ok else "FAIL"


def get_exception_code(resp) -> Optional[int]:
    # pymodbus ExceptionResponse usually exposes .exception_code
    return getattr(resp, "exception_code", None)


def is_exception(resp) -> bool:
    # for pymodbus responses, .isError() indicates exception / error response
    return hasattr(resp, "isError") and resp.isError()


@dataclass
class TestResult:
    name: str
    ok: bool
    details: str = ""


# ---------- modbus tests ----------

def test_connect(client: ModbusTcpClient) -> TestResult:
    ok = client.connect()
    return TestResult("Connect", ok, "connected" if ok else "connect() returned False")


def test_coil_toggle(
    client: ModbusTcpClient,
    device_id: int,
    coil_offset: int,
    settle_s: float = 0.15,
) -> TestResult:
    # Write True, read, then write False, read
    w1 = client.write_coil(coil_offset, True, device_id=device_id)
    time.sleep(settle_s)
    r1 = client.read_coils(coil_offset, count=1, device_id=device_id)

    w2 = client.write_coil(coil_offset, False, device_id=device_id)
    time.sleep(settle_s)
    r2 = client.read_coils(coil_offset, count=1, device_id=device_id)

    if is_exception(w1) or is_exception(w2) or is_exception(r1) or is_exception(r2):
        return TestResult(
            "Coil write/read (single)",
            False,
            f"w1={w1} r1={r1} w2={w2} r2={r2}",
        )

    v1 = bool(r1.bits[0])
    v2 = bool(r2.bits[0])
    ok = (v1 is True) and (v2 is False)

    return TestResult(
        "Coil write/read (single)",
        ok,
        f"offset={coil_offset} -> after True:{v1} after False:{v2}",
    )


def test_block_scan_bits(
    client: ModbusTcpClient,
    device_id: int,
    start: int,
    count: int,
) -> TestResult:
    c = client.read_coils(start, count=count, device_id=device_id)
    d = client.read_discrete_inputs(start, count=count, device_id=device_id)

    if is_exception(c) or is_exception(d):
        return TestResult(
            "Block scan bits (coils+discretes)",
            False,
            f"coils={c} discrete={d}",
        )

    ok = (len(c.bits) == count) and (len(d.bits) == count)
    details = f"range={start}..{start+count-1}, coils_len={len(c.bits)}, discrete_len={len(d.bits)}"
    return TestResult("Block scan bits (coils+discretes)", ok, details)


def test_holding_write_read(
    client: ModbusTcpClient,
    device_id: int,
    offsets: Sequence[int],
    values: Sequence[int],
    settle_s: float = 0.15,
) -> TestResult:
    # writes then reads each offset back
    parts = []
    ok_all = True

    for off, val in zip(offsets, values):
        w = client.write_register(off, val, device_id=device_id)
        time.sleep(settle_s)
        r = client.read_holding_registers(off, count=1, device_id=device_id)

        if is_exception(w) or is_exception(r):
            ok_all = False
            parts.append(f"off={off} write={w} read={r}")
            continue

        got = int(r.registers[0])
        ok = (got == int(val))
        ok_all = ok_all and ok
        parts.append(f"off={off} wrote={val} read={got}")

    return TestResult("Holding write/read (mapped)", ok_all, " | ".join(parts))


def test_unmapped_illegal_address_code2(
    client: ModbusTcpClient,
    device_id: int,
    read_offset: int,
    write_offset: int,
) -> TestResult:
    rr = client.read_holding_registers(read_offset, count=1, device_id=device_id)
    wr = client.write_register(write_offset, 16, device_id=device_id)

    # Both should be exception responses with code 2
    ok = True
    parts = []

    if not is_exception(rr):
        ok = False
        parts.append(f"read(off={read_offset}) expected exception, got OK: {rr}")
    else:
        code = get_exception_code(rr)
        ok = ok and (code == 2)
        parts.append(f"read(off={read_offset}) exception_code={code} resp={rr}")

    if not is_exception(wr):
        ok = False
        parts.append(f"write(off={write_offset}) expected exception, got OK: {wr}")
    else:
        code = get_exception_code(wr)
        ok = ok and (code == 2)
        parts.append(f"write(off={write_offset}) exception_code={code} resp={wr}")

    return TestResult("Unmapped registers -> exception code 2", ok, " | ".join(parts))


# ---------- main ----------

def main() -> int:
    ap = argparse.ArgumentParser(description="GridGhost Modbus TCP smoke test (pymodbus).")
    ap.add_argument("--host", default="127.0.0.1", help="Target host (default: 127.0.0.1)")
    ap.add_argument("--port", type=int, default=1502, help="Target port (default: 1502)")
    ap.add_argument("--device-id", type=int, default=1, help="Modbus device_id / unit id (default: 1)")

    ap.add_argument("--coil-offset", type=int, default=101, help="Coil offset to toggle (default: 101 for 00102)")
    ap.add_argument("--bit-scan-start", type=int, default=96, help="Start offset for coils/discretes scan (default: 96)")
    ap.add_argument("--bit-scan-count", type=int, default=24, help="Count for coils/discretes scan (default: 24)")

    ap.add_argument("--hr-offsets", default="10,12", help="Comma-separated mapped holding offsets (default: 10,12)")
    ap.add_argument("--hr-values", default="123,456", help="Comma-separated values to write for mapped HR (default: 123,456)")

    ap.add_argument("--unmapped-read", type=int, default=11, help="Unmapped HR offset to read (default: 11)")
    ap.add_argument("--unmapped-write", type=int, default=20, help="Unmapped HR offset to write (default: 20)")

    ap.add_argument("--timeout", type=float, default=2.0, help="Modbus timeout seconds (default: 2.0)")
    ap.add_argument("--expect-stopped", action="store_true",
                    help="Only check that TCP port is closed (use after disabling device in UI).")
    args = ap.parse_args()

    # Quick TCP check
    if args.expect_stopped:
        closed = not tcp_open(args.host, args.port, timeout=0.8)
        print(f"[{fmt_ok(closed)}] Port closed check: {args.host}:{args.port}")
        return 0 if closed else 1

    if not tcp_open(args.host, args.port, timeout=0.8):
        print(f"[FAIL] TCP port not open: {args.host}:{args.port}")
        return 1

    hr_offsets = [int(x.strip()) for x in args.hr_offsets.split(",") if x.strip()]
    hr_values = [int(x.strip()) for x in args.hr_values.split(",") if x.strip()]
    if len(hr_offsets) != len(hr_values):
        print("[FAIL] --hr-offsets and --hr-values must have the same length")
        return 1

    results: list[TestResult] = []
    client = ModbusTcpClient(args.host, port=args.port, timeout=args.timeout)

    try:
        res = test_connect(client)
        results.append(res)
        if not res.ok:
            # no point continuing
            raise RuntimeError("connect failed")

        results.append(test_coil_toggle(client, args.device_id, args.coil_offset))
        results.append(test_block_scan_bits(client, args.device_id, args.bit_scan_start, args.bit_scan_count))
        results.append(test_holding_write_read(client, args.device_id, hr_offsets, hr_values))
        results.append(test_unmapped_illegal_address_code2(client, args.device_id, args.unmapped_read, args.unmapped_write))

    except Exception as e:
        results.append(TestResult("Smoke test runtime", False, repr(e)))
    finally:
        try:
            client.close()
        except Exception:
            pass

    all_ok = True
    print("\n=== GridGhost Smoke Test Results ===")
    for r in results:
        all_ok = all_ok and r.ok
        line = f"[{fmt_ok(r.ok)}] {r.name}"
        if r.details:
            line += f" — {r.details}"
        print(line)

    print("\nOverall:", fmt_ok(all_ok))
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
