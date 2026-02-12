import argparse
import ipaddress
import socket
from concurrent.futures import ThreadPoolExecutor, as_completed

from pymodbus.client import ModbusTcpClient
from pymodbus.exceptions import ModbusIOException


def tcp_open(host: str, port: int, timeout: float) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except Exception:
        return False


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


def probe_modbus(host: str, port: int, device_id: int, timeout: float):
    # check porta
    if not tcp_open(host, port, timeout=min(timeout, 0.4)):
        return None

    c = ModbusTcpClient(host, port=port, timeout=timeout)
    try:
        if not c.connect():
            return {"host": host, "port": port, "ok": False, "error": "connect failed"}

        # quick check: prova a leggere 1 HR0 per validare che il server risponda
        try:
            r = c.read_holding_registers(0, count=1, device_id=device_id)
            if hasattr(r, "isError") and r.isError():
                # può essere anche IllegalDataAddress ma almeno risponde
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
    Prova letture in range [start,end] a blocchi 'step'.
    Ritorna per ogni kind la lista degli address "hit" (inizio blocco) che rispondono senza errore.
    """
    kinds = []
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
                    # no response -> consideriamo errore duro e stoppiamo discovery su questo host
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
    Stampa i valori per i blocchi "OK" trovati dalla discovery.
    - hits: dict tipo {"holding":[0,10,...], "input":[...], "coils":[...], "discrete":[...]}
    - step: size blocco letto (stesso usato in discovery)
    - dump_max: limite valori stampati per tipo
    """
    c = ModbusTcpClient(host, port=port, timeout=timeout)
    try:
        if not c.connect():
            print(f"  dump: connect failed")
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

                # registers (holding/input) oppure bits (coils/discrete)
                vals = r.registers if hasattr(r, "registers") else r.bits

                for i, v in enumerate(vals):
                    addr = a0 + i

                    if k in ("holding", "input"):
                        # 16-bit
                        vv = int(v)
                        print(f"  {k}[{addr}] = {vv} (0x{vv:04X})")
                    else:
                        # bit
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
    # "0-200"
    a, b = s.split("-", 1)
    return int(a), int(b)


def main():
    ap = argparse.ArgumentParser(description="Modbus TCP scanner + readable map discovery (pymodbus 3.12)")
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
                    help="ferma la discovery dopo N letture OK (0 = disattiva)")
    ap.add_argument("--show-errors", action="store_true",
                    help="mostra errori per host/porta non validi")
    ap.add_argument("--dump", action="store_true",
                help="stampa anche i valori letti per i blocchi trovati (richiede --discover)")
    ap.add_argument("--dump-max", type=int, default=200,
                    help="massimo numero di valori stampati per ogni tipo (holding/input/coils/discrete)")

    args = ap.parse_args()

    ports = parse_ports(args.ports)
    start, end = parse_range(args.scan_range)
    stop_after_hits = None if args.stop_after_hits == 0 else args.stop_after_hits

    # targets
    if "/" in args.targets:
        net = ipaddress.ip_network(args.targets, strict=False)
        hosts = [str(h) for h in net.hosts()]
    else:
        hosts = [args.targets]

    # 1) scan host:port
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

    # 2) discovery readable
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
                    # stampa i primi hit (indirizzi di inizio blocco)
                    preview = ", ".join(str(x) for x in lst[:20])
                    more = "" if len(lst) <= 20 else f" (+{len(lst)-20} altri)"
                    print(f"  {k}: OK su blocchi starting at [{preview}]{more}")

            # facoltativo: prova a leggere e mostrare il primo blocco “OK” per ciascun kind
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
                        try: c.close()
                        except: pass


if __name__ == "__main__":
    main()