# Modbus TCP Scanner (pymodbus 3.12) — README

Questo script serve per:
- trovare rapidamente quali endpoint **Modbus TCP** sono attivi (host/porta)
- (opzionale) fare una piccola **discovery** per capire *cosa riesce a leggere* (holding/input/coils/discrete) su un range di address

È pensato per testing locale/LAN (es. simulatori come GridGhost).

---

## Requisiti

- Python 3.10+ (consigliato 3.12)
- pymodbus **3.12.x**

Install:

```bash
pip install pymodbus
````

Verifica versione:

```bash
python -c "import pymodbus; print(pymodbus.__version__)"
```

---

## Avvio rapido

Esempio: scan su localhost, porte 1502–1510, device_id=1

```bash
python modbus_scan.py --targets 127.0.0.1 --ports 1502-1510 --device-id 1
```

Output atteso:

* `[MODBUS UP] 127.0.0.1:1502 (...)`
  significa che il server risponde a richieste Modbus con `device_id` indicato.

---

## Parametri principali

* `--targets`
  IP singolo o subnet CIDR
  Esempi:

  * `127.0.0.1`
  * `192.168.1.0/24`

* `--ports`
  Porte singole o range.
  Esempi:

  * `1502`
  * `1502,502`
  * `1502-1510`
  * `1502,1503,1504-1510`

* `--device-id`
  Unit ID / Device ID Modbus (nel tuo caso spesso `1`).

* `--timeout`
  Timeout in secondi (se hai no-response casuali aumenta a 2.0 o 3.0).

* `--workers`
  Numero di thread per lo scan (più alto = più veloce, ma più carico).

* `--show-errors`
  Mostra anche i casi in cui la porta è aperta ma non risponde correttamente.

---

## Discovery: capire cosa è leggibile

Per fare discovery attiva il flag `--discover`.

Esempio base: holding + input, range 0–200, blocchi da 10

```bash
python modbus_scan.py --targets 127.0.0.1 --ports 1502-1510 --device-id 1 --discover --mode both --scan-range 0-200 --step 10
```

### Parametri discovery

* `--discover`
  Abilita la discovery dei blocchi leggibili.

* `--mode`
  Cosa scansionare:

  * `holding`  (Holding Registers)
  * `input`    (Input Registers)
  * `coils`    (Coils)
  * `discrete` (Discrete Inputs)
  * `both`     (holding + input)
  * `all`      (holding + input + coils + discrete)

* `--scan-range`
  Range address da testare, formato `START-END` (inclusivo).
  Esempio: `0-200`

* `--step`
  Dimensione del blocco letto e passo di avanzamento.

  * `step=10` è veloce ma meno preciso (legge blocchi da 10 e salta di 10)
  * `step=1` è preciso ma più lento (legge address per address)

* `--stop-after-hits`
  Ferma la discovery dopo N letture OK totali (utile per non scansionare tutto).

  * default: `30`
  * per disabilitare: `--stop-after-hits 0`

Output discovery:

* ti indica per ogni area (holding/input/coils/discrete) i “blocchi” che hanno risposto OK
* stampa anche un “sample” del primo blocco OK trovato (prime 10 valori)

---

## Esempi utili

### 1) Solo trovare endpoint UP (veloce)

```bash
python modbus_scan.py --targets 127.0.0.1 --ports 1502-1510 --device-id 1
```

### 2) Scan LAN (attenzione: solo rete di test)

```bash
python modbus_scan.py --targets 192.168.1.0/24 --ports 502,1502 --device-id 1 --show-errors
```

### 3) Discovery “precisa” su range piccolo

```bash
python modbus_scan.py --targets 127.0.0.1 --ports 1502 --device-id 1 --discover --mode all --scan-range 0-50 --step 1
```

### 4) Se vedi “no response” o errori casuali

* aumenta timeout:

```bash
python modbus_scan.py --targets 127.0.0.1 --ports 1502-1510 --device-id 1 --timeout 2.5 --show-errors
```

* riduci workers (soprattutto su subnet grandi):

```bash
python modbus_scan.py --targets 192.168.1.0/24 --ports 502 --device-id 1 --workers 32 --timeout 2.0
```

---

## Note tecniche (importanti)

* Questo script è compatibile con **pymodbus 3.12** (che usa `device_id` e `count` keyword-only).
* Il fatto che una porta sia “LISTENING” non garantisce che sia Modbus: la prova vera è la risposta a una richiesta Modbus.
* Se un `device_id` non è accettato, alcuni server non rispondono affatto (timeout), quindi è normale vedere “no response”.
* Se leggi address che non esistono, un server può rispondere con “IllegalDataAddress”: significa che Modbus è vivo, ma l’address non è mappato.

---

## Troubleshooting

### “Non trova nulla ma io vedo il server attivo”

1. Verifica porta aperta:

* PowerShell:

  ```powershell
  Test-NetConnection 127.0.0.1 -Port 1502
  ```

2. Prova `--show-errors` e aumenta `--timeout`.
3. Assicurati che `--device-id` sia corretto (spesso 1).
4. Se scanni una subnet, controlla firewall e routing.

---

## Licenza / uso

Script pensato per testing interno in ambiente di sviluppo.
