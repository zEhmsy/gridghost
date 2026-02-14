#!/usr/bin/env python3
"""
Modbus TCP scanner + discovery + dump + Niagara-first point verification/decoding.
Compatible with pymodbus 3.12.x (device_id + count keyword-only).

Features:
- Scan host(s)/subnet + ports
- Optional discovery of readable ranges (holding/input/coils/discrete)
- Optional raw dump of discovered blocks
- Optional points verification with decoding:
  - 16-bit scaled registers (int16/uint16 + scale/offset)
  - packed booleans in registers (bit)
  - enum bitfields in registers (startBit + bitLength + optional mapping)
- Optional Niagara-style addressing (30001/40001) for input/holding points.
"""

import argparse
import ipaddress
import json
import socket
from concurrent.futures import ThreadPoolExecutor, as_completed

from pymodbus.client import ModbusTcpClient
from pymodbus.exceptions import ModbusIOException


# -----------------------------
# TCP quick check
# -----------------------------
def tcp_open(host: str, port: int, timeout: float) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except Exception:
        return False


# -----------------------------
# Modbus reads (pymodbus 3.12)
# -----------------------------
def _read(client, kind: str, addr: int, count: int, device_id: int):
    """
    kind: holding | input | coils | discrete
    """
    if kind == "holding":
        return client.read_holding_registers(addr, count=count, device_id=device_id)
    if kind == "input":
        return client.read_input_registers(addr, count=count, device_id=device_id)
    if kind == "coils":
        return client.read_coils(addr, count=count, device_id=device_id)
    if kind == "discrete":
        return client.read_discrete_inputs(addr, count=count, device_id=device_id)
    raise ValueError(f"unknown kind: {kind}")


# -----------------------------
# Niagara-style address normalization
# -----------------------------
def normalize_address(kind: str, address: int, niagara: bool):
    """
    With niagara=True accept Niagara-style addressing:
      - input registers:    30001 -> offset 0
      - holding registers:  40001 -> offset 0
      - discrete inputs:    10001 -> offset 0
      - coils:              00001 -> offset 0   (so coil 102 -> offset 101)
    """
    if not niagara:
        return kind, address

    # normalize kind aliases
    if kind == "coil":
        kind = "coils"

    # input regs
    if kind == "input" and 30001 <= address <= 39999:
        return "input", address - 30001

    # holding regs
    if kind == "holding" and 40001 <= address <= 49999:
        return "holding", address - 40001

    # discrete inputs (1xxxx)
    if kind == "discrete" and 10001 <= address <= 19999:
        return "discrete", address - 10001

    # coils (0xxxx). In UI often shown as 00001..09999
    if kind == "coils":
        # if user passes already 1..9999 as "coil number", treat 1->offset0
        if 1 <= address <= 9999:
            return "coils", address - 1
        # else leave it as-is
        return "coils", address

    return kind, address



# -----------------------------
# Decoding helpers
# -----------------------------
def to_int16(v: int) -> int:
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def to_uint16(v: int) -> int:
    return v & 0xFFFF


def get_bit(v: int, bit: int) -> int:
    return 1 if ((v >> bit) & 1) else 0


def get_bitfield(v: int, start: int, length: int) -> int:
    mask = (1 << length) - 1
    return (v >> start) & mask


def read_point_decoded(client, p: dict, device_id: int, niagara: bool):
    """
    Point schema examples (points.json):
    {
      "name":"SupplyTemp",
      "kind":"input",
      "address":30002,
      "dataType":"int16",
      "scale":0.1,
      "offset":0.0,
      "unit":"degC"
    }

    Packed boolean in register:
    { "name":"AlarmPacked", "kind":"holding", "address":40020, "bit":3 }

    Enum bitfield:
    {
      "name":"FanMode", "kind":"holding", "address":40021,
      "bitfield":{"start":4,"len":3},
      "enum":{"0":"off","1":"low","2":"med","3":"high"}
    }

    Coils/discrete:
    { "name":"EnableCmd", "kind":"coil", "address":1 }
    """
    name = p.get("name", "unnamed")
    kind = p["kind"].lower()
    addr = int(p["address"])

    # normalize kind naming
    if kind == "coil":
        kind = "coils"
    if kind == "discrete_input":
        kind = "discrete"

    kind, off = normalize_address(kind, addr, niagara)

    # (1) packed boolean / enum bitfield within a register
    if kind in ("holding", "input") and ("bit" in p or "bitfield" in p):
        r = _read(client, kind, off, 1, device_id)
        if hasattr(r, "isError") and r.isError():
            return name, False, str(r)

        raw = int(r.registers[0])

        if "bit" in p:
            b = int(p["bit"])
            if not (0 <= b <= 15):
                return name, False, f"invalid bit index {b} (must be 0..15)"
            return name, True, {"kind": kind, "offset": off, "raw_u16": raw, "bit": b, "value": get_bit(raw, b)}

        bf = p["bitfield"]
        start = int(bf["start"])
        length = int(bf["len"])
        if not (0 <= start <= 15) or not (1 <= length <= 16) or (start + length > 16):
            return name, False, f"invalid bitfield start={start} len={length} (must fit in 0..15)"

        val = get_bitfield(raw, start, length)
        enum_map = p.get("enum", {})
        label = enum_map.get(str(val))
        out = {"kind": kind, "offset": off, "raw_u16": raw, "bitfield": (start, length), "value": val}
        if label is not None:
            out["label"] = label
        return name, True, out

    # (2) coils/discrete direct boolean
    if kind in ("coils", "discrete"):
        r = _read(client, kind, off, 1, device_id)
        if hasattr(r, "isError") and r.isError():
            return name, False, str(r)
        return name, True, {"kind": kind, "offset": off, "value": int(bool(r.bits[0]))}

    # (3) 16-bit numeric scaled (holding/input)
    if kind in ("holding", "input"):
        r = _read(client, kind, off, 1, device_id)
        if hasattr(r, "isError") and r.isError():
            return name, False, str(r)

        raw_u16 = int(r.registers[0])
        dt = (p.get("dataType") or "uint16").lower()
        scale = float(p.get("scale", 1.0))
        offset = float(p.get("offset", 0.0))
        unit = p.get("unit", "")

        if dt == "int16":
            raw = to_int16(raw_u16)
        else:
            raw = to_uint16(raw_u16)

        human = raw * scale + offset
        return name, True, {
            "kind": kind,
            "offset": off,
            "raw": raw,
            "raw_u16_hex": f"0x{raw_u16:04X}",
            "scaled": human,
            "unit": unit,
            "scale": scale,
            "offset": offset,
        }

    return name, False, f"Unsupported point definition: {p}"


# -----------------------------
# Probe & discovery
# -----------------------------
def probe_modbus(host: str, port: int, device_id: int, timeout: float):
    # check port quickly
    if not tcp_open(host, port, timeout=min(timeout, 0.4)):
        return None

    c = ModbusTcpClient(host, port=port, timeout=timeout)
    try:
        if not c.connect():
            return {"host": host, "port": port, "ok": False, "error": "connect failed"}

        # quick check: try HR0
        try:
            r = c.read_holding_registers(0, count=1, device_id=device_id)
            if hasattr(r, "isError") and r.isError():
                # could be IllegalDataAddress; still means server responds
                return {"host": host, "port": port, "ok": True, "note": f"responds but HR0 err: {r}"}
            return {"host": host, "port": port, "ok": True, "note": "responds"}
        except ModbusIOException as e:
            return {"host": host, "port": port, "ok": False, "error": f"no response ({e})"}

    except Exception as e:
        return {"host": host, "port": port, "ok": False, "error": repr(e)}
    finally:
        try:
            c.close()
        except Exception:
            pass


def discover_readable(host: str, port: int, device_id: int, mode: str,
                      start: int, end: int, step: int, timeout: float,
                      stop_after_hits: int | None):
    """
    Try reads in range [start,end] stepping by 'step'.
    Return for each kind the list of starting addresses that respond without error.
    """
    if mode == "both":
        kinds = ["holding", "input"]
    elif mode == "all":
        kinds = ["holding", "input", "coils", "discrete"]
    else:
        kinds = [mode]

    hits = {k: [] for k in kinds}
    errors = {k: 0 for k in kinds}

    c = ModbusTcpClient(host, port=port, timeout=timeout)
    try:
        if not c.connect():
            return {"ok": False, "error": "connect failed"}

        total_hits = 0
        addr = start
        while addr <= end:
            for k in kinds:
                try:
                    r = _read(c, k, addr, step, device_id)
                    if hasattr(r, "isError") and r.isError():
                        errors[k] += 1
                    else:
                        hits[k].append(addr)
                        total_hits += 1
                        if stop_after_hits and total_hits >= stop_after_hits:
                            return {"ok": True, "hits": hits, "errors": errors}
                except ModbusIOException:
                    return {"ok": False, "error": "no response (timeout) during discovery"}
                except Exception:
                    errors[k] += 1
            addr += step

        return {"ok": True, "hits": hits, "errors": errors}
    finally:
        try:
            c.close()
        except Exception:
            pass


def dump_values(host: str, port: int, device_id: int, hits: dict, step: int, timeout: float, dump_max: int):
    """
    Print raw values for discovered blocks.
    """
    c = ModbusTcpClient(host, port=port, timeout=timeout)
    try:
        if not c.connect():
            print("  dump: connect failed")
            return

        for k, starts in hits.items():
            if not starts:
                continue

            print(f"\n  === DUMP {k.upper()} ===")
            printed = 0

            for a0 in starts:
                try:
                    r = _read(c, k, a0, step, device_id)
                except ModbusIOException:
                    print("  dump: no response (timeout)")
                    return

                if hasattr(r, "isError") and r.isError():
                    continue

                vals = r.registers if hasattr(r, "registers") else r.bits

                for i, v in enumerate(vals):
                    addr = a0 + i
                    if k in ("holding", "input"):
                        vv = int(v)
                        print(f"  {k}[{addr}] = {vv} (0x{vv:04X})")
                    else:
                        print(f"  {k}[{addr}] = {int(bool(v))}")

                    printed += 1
                    if printed >= dump_max:
                        print(f"  (limit raggiunto: {dump_max} valori)")
                        break

                if printed >= dump_max:
                    break
    finally:
        try:
            c.close()
        except Exception:
            pass


# -----------------------------
# Parsing helpers
# -----------------------------
def parse_ports(s: str):
    ports = set()
    for part in s.split(","):
        part = part.strip()
        if not part:
            continue
        if "-" in part:
            a, b = part.split("-", 1)
            ports.update(range(int(a), int(b) + 1))
        else:
            ports.add(int(part))
    return sorted(ports)


def parse_range(s: str):
    a, b = s.split("-", 1)
    return int(a), int(b)


# -----------------------------
# Main
# -----------------------------
def main():
    ap = argparse.ArgumentParser(description="Modbus TCP scanner + discovery + dump + decoded points (pymodbus 3.12)")
    ap.add_argument("--targets", default="127.0.0.1",
                    help="IP singolo o subnet CIDR, es. 127.0.0.1 oppure 192.168.1.0/24")
    ap.add_argument("--ports", default="1502,502",
                    help="porte da testare, es. 1502,1503,1504-1510")
    ap.add_argument("--device-id", type=int, default=1,
                    help="Modbus device_id (Unit ID)")
    ap.add_argument("--timeout", type=float, default=1.5,
                    help="timeout in secondi")
    ap.add_argument("--workers", type=int, default=64,
                    help="thread worker")
    ap.add_argument("--show-errors", action="store_true",
                    help="mostra errori per host/porta non validi")

    ap.add_argument("--discover", action="store_true",
                    help="oltre a trovare i device, prova anche a scoprire cosa è leggibile")
    ap.add_argument("--mode", default="both",
                    choices=["holding", "input", "coils", "discrete", "both", "all"],
                    help="cosa scansionare in discovery")
    ap.add_argument("--scan-range", default="0-200",
                    help="range address per discovery, es. 0-200")
    ap.add_argument("--step", type=int, default=10,
                    help="block size e passo della discovery (più alto = più veloce, meno dettagli)")
    ap.add_argument("--stop-after-hits", type=int, default=30,
                    help="ferma la discovery dopo N letture OK totali (0 = disattiva)")
    ap.add_argument("--dump", action="store_true",
                    help="stampa i valori grezzi per i blocchi trovati (richiede --discover)")
    ap.add_argument("--dump-max", type=int, default=200,
                    help="massimo numero di valori stampati nel dump grezzo")

    ap.add_argument("--points", default="",
                    help="path a points.json per verificare punti decodificati (scaled/packed/enum)")
    ap.add_argument("--niagara", action="store_true",
                    help="interpreta address holding/input come Niagara-style (30001/40001)")

    args = ap.parse_args()

    ports = parse_ports(args.ports)
    start, end = parse_range(args.scan_range)
    stop_after_hits = None if args.stop_after_hits == 0 else args.stop_after_hits

    # targets list
    if "/" in args.targets:
        net = ipaddress.ip_network(args.targets, strict=False)
        hosts = [str(h) for h in net.hosts()]
    else:
        hosts = [args.targets]

    # (1) scan host:port
    futures = []
    candidates = []
    with ThreadPoolExecutor(max_workers=args.workers) as ex:
        for host in hosts:
            for port in ports:
                futures.append(ex.submit(probe_modbus, host, port, args.device_id, args.timeout))

        for f in as_completed(futures):
            res = f.result()
            if not res:
                continue
            if res["ok"]:
                print(f"[MODBUS UP] {res['host']}:{res['port']} ({res.get('note','')})")
                candidates.append((res["host"], res["port"]))
            else:
                if args.show_errors:
                    print(f"[NO/ERR] {res['host']}:{res['port']} -> {res['error']}")

    # (2) points verify/decoded
    if args.points and candidates:
        with open(args.points, "r", encoding="utf-8") as f:
            cfg = json.load(f)

        dev_id = int(cfg.get("device_id", args.device_id))
        points = cfg.get("points", [])

        print("\n=== POINTS VERIFY (decoded) ===")
        for host, port in candidates:
            c = ModbusTcpClient(host, port=port, timeout=args.timeout)
            try:
                if not c.connect():
                    print(f"- {host}:{port} connect failed")
                    continue
                print(f"\n- {host}:{port} device_id={dev_id} (niagara_addr={args.niagara})")
                for p in points:
                    try:
                        name, ok, out = read_point_decoded(c, p, dev_id, args.niagara)
                        if ok:
                            print(f"  {name}: {out}")
                        else:
                            print(f"  {name}: ERR {out}")
                    except ModbusIOException as e:
                        print(f"  {p.get('name','unnamed')}: NO RESPONSE ({e})")
            finally:
                try:
                    c.close()
                except Exception:
                    pass

    # (3) discovery readable + optional raw dump
    if args.discover and candidates:
        print("\n=== DISCOVERY READABLE MAP ===")
        for host, port in candidates:
            d = discover_readable(
                host, port, args.device_id, args.mode,
                start, end, args.step, args.timeout, stop_after_hits
            )
            if not d["ok"]:
                print(f"- {host}:{port} discovery failed -> {d['error']}")
                continue

            hits = d["hits"]

            if args.dump:
                dump_values(host, port, args.device_id, hits, args.step, args.timeout, args.dump_max)

            print(f"\n- {host}:{port} device_id={args.device_id} range={start}-{end} step={args.step} mode={args.mode}")
            for k, lst in hits.items():
                if not lst:
                    print(f"  {k}: nessun blocco leggibile trovato")
                else:
                    preview = ", ".join(str(x) for x in lst[:20])
                    more = "" if len(lst) <= 20 else f" (+{len(lst)-20} altri)"
                    print(f"  {k}: OK su blocchi starting at [{preview}]{more}")

            # show one sample per kind
            for k, lst in hits.items():
                if lst:
                    a0 = lst[0]
                    c = ModbusTcpClient(host, port=port, timeout=args.timeout)
                    try:
                        if c.connect():
                            r = _read(c, k, a0, args.step, args.device_id)
                            if hasattr(r, "isError") and (not r.isError()):
                                vals = r.registers if hasattr(r, "registers") else r.bits
                                vals_preview = ", ".join(str(x) for x in vals[:10])
                                print(f"    sample {k} @ {a0}: {vals_preview}")
                    finally:
                        try:
                            c.close()
                        except Exception:
                            pass


if __name__ == "__main__":
    main()