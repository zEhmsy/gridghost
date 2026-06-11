# gridGhostImporter

Niagara module for importing GridGhost-generated Modbus manifests.

## Workflow

1. Build and deploy the Niagara module on the VM:

   ```bash
   SIGN_PASS='your-keystore-password' ./deploy.sh [vm_password]
   ```

2. Close and reopen Workbench after deployment.
3. In Workbench, add `gridGhostImporter:GridGhostImportService` under Services.
4. In GridGhost, open the Devices view.
5. Set **Export host** to the IP address Niagara can reach from the station/VM.
6. Click **Export** and save `niagara-manifest.json` wherever convenient.
7. In the Workbench manager for `GridGhostImportService`, click **Import Now**.
8. Select the manifest JSON from the file picker.

The manager uploads the selected file into the station shared folder and then runs the import automatically.

## Service Defaults

- `manifestOrd`: `file:^shared/gridghost-niagara-manifest.json`
- `stationRoot`: `station:|slot:/Drivers`
- `networkSlotName`: `GridGhost_Modbus`

`Validate Manifest` reads `manifestOrd` without modifying the station. `Import Now` opens a local file picker, uploads the selected JSON, updates `manifestOrd`, and creates/updates the Niagara Modbus components.

## Manifest Transfer Helper

For scripted workflows, `push-manifest.sh` can still copy a manifest directly to a station shared folder:

```bash
./push-manifest.sh [vm_password] [station_name] [manifest_path]
```

The service can then read:

`file:^shared/gridghost-niagara-manifest.json`

## Modbus Point Import

The importer creates or updates:

- `BModbusTcpNetwork` / `BModbusTcpDevice`, or gateway-compatible devices when an existing gateway topology is reused;
- numeric and boolean Modbus client points under each device's `points` extension;
- Niagara proxy `dataAddress` in **Modbus** format, for example `40011`, `30001`, `00102`, `10101`;
- register/status type according to the manifest point kind.

Read-only Modbus ranges are forced to read-only Niagara point classes:

- `Input` / `3xxxx` -> read-only numeric point;
- `Discrete` / `1xxxx` -> read-only boolean point;
- `Holding` / `4xxxx` -> writable numeric point when manifest access allows it;
- `Coil` / `0xxxx` -> writable boolean point when manifest access allows it.

The service also sets the device ping address from the first valid input/holding register in the manifest.

## Notes

- GridGhost must be running for Niagara to poll values after import.
- `Export host` is the IP Niagara uses to reach GridGhost. It is not necessarily the same as GridGhost's bind IP.
- The Workbench file picker and confirmation dialog are Swing windows, so Workbench may label them as "Java Applet Window".
- Enum and bitfield metadata is preserved on imported points for future refinement.
