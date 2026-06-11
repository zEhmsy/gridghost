/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */
package com.sitecVendor.gridGhostImporter;

import com.tridium.modbusCore.client.BModbusClientDevice;
import com.tridium.modbusCore.client.point.BModbusClientBooleanProxyExt;
import com.tridium.modbusCore.client.point.BModbusClientNumericProxyExt;
import com.tridium.modbusCore.client.point.BModbusClientPointFolder;
import com.tridium.modbusCore.client.point.BModbusClientProxyExt;
import com.tridium.modbusCore.datatypes.BFlexAddress;
import com.tridium.modbusCore.enums.BAddressFormatEnum;
import com.tridium.modbusCore.enums.BDataTypeEnum;
import com.tridium.modbusCore.enums.BRegisterTypeEnum;
import com.tridium.modbusCore.enums.BStatusTypeEnum;
import com.tridium.modbusTcp.BModbusTcpDevice;
import com.tridium.modbusTcp.BModbusTcpGateway;
import com.tridium.modbusTcp.BModbusTcpGatewayDevice;
import com.tridium.modbusTcp.BModbusTcpNetwork;

import javax.baja.control.BBooleanPoint;
import javax.baja.control.BBooleanWritable;
import javax.baja.control.BControlPoint;
import javax.baja.control.BNumericPoint;
import javax.baja.control.BNumericWritable;
import javax.baja.file.BIFile;
import javax.baja.naming.BOrd;
import javax.baja.nre.annotations.NiagaraType;
import javax.baja.nre.annotations.NoSlotomatic;
import javax.baja.sys.*;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.nio.file.Files;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Map;
import java.util.logging.Logger;

@NiagaraType
@NoSlotomatic
public final class BGridGhostImportService extends BComponent implements BIService {

    private static final Logger LOG = Logger.getLogger(BGridGhostImportService.class.getName());
    private static final String EXPECTED_SCHEMA = "gridghost.niagara.v1";
    private static final String SHARED_DIR_NAME = "shared";

    /*+ ------------ BEGIN BAJA AUTO GENERATED CODE ------------ +*/
    /*@ $com.sitecVendor.gridGhostImporter.BGridGhostImportService(2979906276)1.0$ @*/
    /* pragma(NiagaraType, 'com.sitecVendor.gridGhostImporter.BGridGhostImportService') */
    /*- ------------ END BAJA AUTO GENERATED CODE -------------- +*/

    public static final Property manifestOrd =
            newProperty(Flags.SUMMARY, BOrd.make("file:^shared/gridghost-niagara-manifest.json"), null);
    public BOrd getManifestOrd() { return (BOrd) get(manifestOrd); }
    public void setManifestOrd(BOrd v) { set(manifestOrd, v, null); }

    public static final Property stationRoot =
            newProperty(Flags.SUMMARY, BOrd.make("station:|slot:/Drivers"), null);
    public BOrd getStationRoot() { return (BOrd) get(stationRoot); }
    public void setStationRoot(BOrd v) { set(stationRoot, v, null); }

    public static final Property networkSlotName =
            newProperty(Flags.SUMMARY, "GridGhost_Modbus", null);
    public String getNetworkSlotName() { return getString(networkSlotName); }
    public void setNetworkSlotName(String v) { setString(networkSlotName, v, null); }

    public static final Property lastImportStatus =
            newProperty(Flags.SUMMARY | Flags.READONLY, "idle", null);
    public String getLastImportStatus() { return getString(lastImportStatus); }
    public void setLastImportStatus(String v) { setString(lastImportStatus, v, null); }

    public static final Property lastError =
            newProperty(Flags.SUMMARY | Flags.READONLY, "", null);
    public String getLastError() { return getString(lastError); }
    public void setLastError(String v) { setString(lastError, v, null); }

    public static final Property lastImportedAt =
            newProperty(Flags.SUMMARY | Flags.READONLY, "", null);
    public String getLastImportedAt() { return getString(lastImportedAt); }
    public void setLastImportedAt(String v) { setString(lastImportedAt, v, null); }

    public static final Property createdCount =
            newProperty(Flags.SUMMARY | Flags.READONLY, 0, null);
    public int getCreatedCount() { return getInt(createdCount); }
    public void setCreatedCount(int v) { setInt(createdCount, v, null); }

    public static final Property updatedCount =
            newProperty(Flags.SUMMARY | Flags.READONLY, 0, null);
    public int getUpdatedCount() { return getInt(updatedCount); }
    public void setUpdatedCount(int v) { setInt(updatedCount, v, null); }

    public static final Property skippedCount =
            newProperty(Flags.SUMMARY | Flags.READONLY, 0, null);
    public int getSkippedCount() { return getInt(skippedCount); }
    public void setSkippedCount(int v) { setInt(skippedCount, v, null); }

    public static final Property pendingUploadName =
            newProperty(Flags.HIDDEN | Flags.TRANSIENT, "", null);
    public String getPendingUploadName() { return getString(pendingUploadName); }
    public void setPendingUploadName(String v) { setString(pendingUploadName, v, null); }

    public static final Property pendingUploadB64 =
            newProperty(Flags.HIDDEN | Flags.TRANSIENT, "", null);
    public String getPendingUploadB64() { return getString(pendingUploadB64); }
    public void setPendingUploadB64(String v) { setString(pendingUploadB64, v, null); }

    public static final Action uploadManifest = newAction(Flags.OPERATOR | Flags.HIDDEN, null);
    public void uploadManifest() { invoke(uploadManifest, null, null); }
    public void doUploadManifest() {
        String name = getPendingUploadName();
        String b64 = getPendingUploadB64();
        if (name == null || name.length() == 0 || b64 == null || b64.length() == 0) {
            setLastError("uploadManifest: missing file name or bytes");
            return;
        }

        try {
            name = safeUploadFileName(name);
            byte[] data = java.util.Base64.getDecoder().decode(b64);
            File sharedDir = getStationSharedDir();
            Files.createDirectories(sharedDir.toPath());
            File dest = new File(sharedDir, name);
            Files.write(dest.toPath(), data);
            setManifestOrd(BOrd.make(sharedFileOrd(name)));
            setLastImportStatus("upload OK: " + name + " (" + data.length + " bytes)");
            setLastError("");
        } catch (Exception e) {
            LOG.warning("[GridGhostImportService] upload failed: " + e.getMessage());
            setLastError("upload failed: " + e.getMessage());
            setLastImportStatus("upload failed");
        } finally {
            setPendingUploadName("");
            setPendingUploadB64("");
        }
    }

    public static final Action validateManifest = newAction(Flags.OPERATOR | Flags.ASYNC, null);
    public void validateManifest() { invoke(validateManifest, null, null); }
    public void doValidateManifest() { runImport(false); }

    public static final Action importNow = newAction(Flags.OPERATOR | Flags.ASYNC, null);
    public void importNow() { invoke(importNow, null, null); }
    public void doImportNow() { runImport(true); }

    @Override
    public void serviceStarted() {
        setLastImportStatus("service started");
    }

    @Override
    public void serviceStopped() {
        setLastImportStatus("service stopped");
    }

    @Override
    public Type[] getServiceTypes() { return new Type[]{ TYPE }; }

    private void runImport(boolean mutate) {
        setLastError("");
        setCreatedCount(0);
        setUpdatedCount(0);
        setSkippedCount(0);

        try {
            Map<String, Object> manifest = loadManifest();
            validateSchema(manifest);

            @SuppressWarnings("unchecked")
            List<Object> devices = (List<Object>) manifest.get("devices");
            int pointCount = countPoints(devices);

            if (!mutate) {
                setLastImportStatus("valid manifest: " + devices.size() + " devices, " + pointCount + " points");
                return;
            }

            BComponent root = resolveRoot();
            BModbusTcpNetwork network = ensureNetwork(root, safeSlot(getNetworkSlotName(), "GridGhost_Modbus"));
            ImportCounters counters = new ImportCounters();

            for (int i = 0; i < devices.size(); i++) {
                Object raw = devices.get(i);
                if (!(raw instanceof Map)) {
                    counters.skipped++;
                    continue;
                }
                @SuppressWarnings("unchecked")
                Map<String, Object> device = (Map<String, Object>) raw;
                importDevice(network, device, counters);
            }

            setCreatedCount(counters.created);
            setUpdatedCount(counters.updated);
            setSkippedCount(counters.skipped);
            setLastImportedAt(new SimpleDateFormat("yyyy-MM-dd HH:mm:ss").format(new Date()));
            setLastImportStatus("import OK: created=" + counters.created
                    + ", updated=" + counters.updated + ", skipped=" + counters.skipped);
        } catch (Exception e) {
            LOG.warning("[GridGhostImportService] import failed: " + e.getMessage());
            setLastError(e.getClass().getSimpleName() + ": " + e.getMessage());
            setLastImportStatus("failed");
        }
    }

    private Map<String, Object> loadManifest() throws Exception {
        String json = readManifestText();
        return NxJson.parseObject(json);
    }

    private String readManifestText() throws Exception {
        BOrd ord = getManifestOrd();
        if (ord == null || BOrd.DEFAULT.equals(ord)) {
            throw new IllegalStateException("manifestOrd is empty");
        }

        String ordText = ord.toString();
        Exception last = null;
        String[] candidates = new String[] {
                ordText,
                ordText.startsWith("station:|") ? ordText.substring("station:|".length()) : null,
                ordText.startsWith("local:|") ? ordText.substring("local:|".length()) : null,
                ordText.startsWith("file:") && ordText.indexOf('|') < 0 ? "station:|" + ordText : null
        };

        for (int i = 0; i < candidates.length; i++) {
            String candidate = candidates[i];
            if (candidate == null || candidate.length() == 0) continue;
            try {
                BValue resolved = (BValue) BOrd.make(candidate).resolve(this, null).get();
                if (resolved instanceof BIFile) {
                    InputStream in = ((BIFile) resolved).getInputStream();
                    try {
                        return readAll(in);
                    } finally {
                        in.close();
                    }
                }
            } catch (Exception e) {
                last = e;
            }
        }

        File fsFile = resolveFileFallback(ordText);
        if (fsFile != null && fsFile.exists() && fsFile.canRead()) {
            InputStream in = new FileInputStream(fsFile);
            try {
                return readAll(in);
            } finally {
                in.close();
            }
        }

        throw new IllegalStateException("Cannot read manifest " + ordText
                + (last != null ? ": " + last.getMessage() : ""));
    }

    private static String readAll(InputStream in) throws Exception {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int r;
        while ((r = in.read(buf)) != -1) out.write(buf, 0, r);
        return new String(out.toByteArray(), "UTF-8");
    }

    private static File resolveFileFallback(String ordText) {
        String clean = stripHostPrefix(ordText).replace('\\', '/');
        if (clean.startsWith("file:^shared/")) {
            String rel = clean.substring("file:^shared/".length());
            return new File(new File(Sys.getStationHome(), SHARED_DIR_NAME), safeRelativePath(rel));
        }
        if (clean.startsWith("file:^")) {
            String rel = clean.substring("file:^".length());
            return new File(Sys.getStationHome(), safeRelativePath(rel));
        }
        if (clean.startsWith("file:!")) {
            String rel = clean.substring("file:!".length());
            return new File(Sys.getNiagaraUserHome(), safeRelativePath(rel));
        }
        if (clean.startsWith("/")) {
            return new File(clean);
        }
        return null;
    }

    private static File getStationSharedDir() {
        return new File(Sys.getStationHome(), SHARED_DIR_NAME);
    }

    public static String sharedFileOrd(String name) {
        return "file:^shared/" + safeUploadFileName(name);
    }

    private static String safeUploadFileName(String name) {
        String safe = name == null ? "" : name.trim().replace('\\', '/');
        int slash = safe.lastIndexOf('/');
        if (slash >= 0) safe = safe.substring(slash + 1);
        safe = safe.replaceAll("[^a-zA-Z0-9_.-]", "_");
        if (safe.length() == 0 || safe.indexOf("..") >= 0) {
            throw new IllegalArgumentException("invalid manifest file name: " + name);
        }
        if (!safe.toLowerCase().endsWith(".json")) safe = safe + ".json";
        return safe;
    }

    private static String stripHostPrefix(String value) {
        if (value.startsWith("station:|")) return value.substring("station:|".length());
        if (value.startsWith("local:|")) return value.substring("local:|".length());
        return value;
    }

    private static String safeRelativePath(String rel) {
        String value = rel == null ? "" : rel.replace('\\', '/');
        while (value.startsWith("/")) value = value.substring(1);
        if (value.indexOf("..") >= 0) throw new IllegalArgumentException("unsafe relative path: " + rel);
        return value.replace('/', File.separatorChar);
    }

    @SuppressWarnings("unchecked")
    private static void validateSchema(Map<String, Object> manifest) {
        String schema = str(manifest, "schema", "");
        if (!EXPECTED_SCHEMA.equals(schema)) {
            throw new IllegalStateException("Unsupported schema: " + schema);
        }
        Object devices = manifest.get("devices");
        if (!(devices instanceof List)) {
            throw new IllegalStateException("Manifest missing devices array");
        }
    }

    private static int countPoints(List<Object> devices) {
        int count = 0;
        for (int i = 0; i < devices.size(); i++) {
            Object raw = devices.get(i);
            if (!(raw instanceof Map)) continue;
            @SuppressWarnings("unchecked")
            Map<String, Object> device = (Map<String, Object>) raw;
            Object points = device.get("points");
            if (points instanceof List) count += ((List<?>) points).size();
        }
        return count;
    }

    private BComponent resolveRoot() throws Exception {
        BOrd rootOrd = getStationRoot();
        BValue root = (BValue) rootOrd.resolve(this, null).get();
        if (!(root instanceof BComponent)) {
            throw new IllegalStateException("stationRoot is not a component: " + rootOrd);
        }
        return (BComponent) root;
    }

    private BModbusTcpNetwork ensureNetwork(BComponent parent, String slotName) throws Exception {
        try {
            BValue existing = parent.get(slotName);
            if (existing instanceof BModbusTcpNetwork) return (BModbusTcpNetwork) existing;
        } catch (Exception ignore) {}

        for (int i = 0; i < parent.getPropertiesArray().length; i++) {
            Property p = parent.getPropertiesArray()[i];
            try {
                BValue v = parent.get(p);
                if (v instanceof BModbusTcpNetwork && !(v instanceof BModbusTcpGateway)) {
                    return (BModbusTcpNetwork) v;
                }
            } catch (Exception ignore) {}
        }

        BModbusTcpNetwork network = new BModbusTcpNetwork();
        String freeSlot = freeSlot(parent, slotName);
        parent.add(freeSlot, network, Flags.SUMMARY);
        return (BModbusTcpNetwork) parent.get(freeSlot);
    }

    private static String freeSlot(BComponent parent, String desired) {
        try {
            if (parent.get(desired) == null) return desired;
        } catch (Exception ignore) {
            return desired;
        }

        for (int i = 2; i < 100; i++) {
            String candidate = desired + i;
            try {
                if (parent.get(candidate) == null) return candidate;
            } catch (Exception ignore) {
                return candidate;
            }
        }
        return desired;
    }

    @SuppressWarnings("unchecked")
    private void importDevice(BModbusTcpNetwork network, Map<String, Object> device, ImportCounters counters) {
        String name = str(device, "name", "GridGhostDevice");
        String host = str(device, "host", str(device, "exportHost", "127.0.0.1"));
        int port = integer(device, "port", 502);
        int deviceAddress = integer(device, "deviceAddress", 1);
        String slotName = safeSlot(name + "_U" + deviceAddress, "GridGhostDevice_U" + deviceAddress);

        try {
            BModbusClientDevice modbusDevice = ensureDevice(network, slotName, host, port, deviceAddress, counters);
            configureDevicePing(modbusDevice, device);
            BComponent pointsContainer = getPointsContainer(modbusDevice);
            if (pointsContainer == null) {
                counters.skipped++;
                setLastError("No points slot on " + slotName);
                return;
            }

            Object rawPoints = device.get("points");
            if (!(rawPoints instanceof List)) return;
            List<Object> points = (List<Object>) rawPoints;
            for (int i = 0; i < points.size(); i++) {
                Object rawPoint = points.get(i);
                if (!(rawPoint instanceof Map)) {
                    counters.skipped++;
                    continue;
                }
                importPoint(pointsContainer, (Map<String, Object>) rawPoint, counters);
            }
        } catch (Exception e) {
            counters.skipped++;
            LOG.warning("[GridGhostImportService] device " + name + " failed: " + e.getMessage());
            setLastError("device " + name + ": " + e.getMessage());
        }
    }

    private BModbusClientDevice ensureDevice(
            BModbusTcpNetwork network,
            String slotName,
            String host,
            int port,
            int deviceAddress,
            ImportCounters counters) throws Exception {
        boolean gatewayMode = network instanceof BModbusTcpGateway;
        if (gatewayMode) applyGatewaySettings((BModbusTcpGateway) network, host, port);

        BModbusClientDevice device = null;
        try {
            BValue existing = network.get(slotName);
            if (existing instanceof BModbusClientDevice) device = (BModbusClientDevice) existing;
        } catch (Exception ignore) {}

        if (device == null) {
            device = gatewayMode ? new BModbusTcpGatewayDevice() : new BModbusTcpDevice();
            applyDeviceSettings(device, host, port, deviceAddress);
            network.add(slotName, device, Flags.SUMMARY);
            counters.created++;
            BValue mounted = network.get(slotName);
            if (mounted instanceof BModbusClientDevice) device = (BModbusClientDevice) mounted;
        } else {
            applyDeviceSettings(device, host, port, deviceAddress);
            counters.updated++;
        }

        putString(device, "gridGhostHost", host);
        putInt(device, "gridGhostPort", port);
        putInt(device, "gridGhostDeviceAddress", deviceAddress);
        return device;
    }

    private static void applyDeviceSettings(BModbusClientDevice device, String host, int port, int deviceAddress) {
        if (device instanceof BModbusTcpDevice) {
            BModbusTcpDevice tcpDevice = (BModbusTcpDevice) device;
            try { if (host != null && !host.equals(tcpDevice.getIpAddress())) tcpDevice.setIpAddress(host); } catch (Exception ignore) {}
            try { if (port > 0 && tcpDevice.getPort() != port) tcpDevice.setPort(port); } catch (Exception ignore) {}
        }
        try {
            if (deviceAddress > 0 && device.getDeviceAddress() != deviceAddress) {
                device.setDeviceAddress(deviceAddress);
            }
        } catch (Exception e) {
            try {
                if (deviceAddress > 0) device.set("deviceAddress", BInteger.make(deviceAddress));
            } catch (Exception ignore) {}
        }
    }

    @SuppressWarnings("unchecked")
    private static void configureDevicePing(BModbusClientDevice device, Map<String, Object> deviceJson) {
        Object rawPoints = deviceJson.get("points");
        if (!(rawPoints instanceof List)) return;

        List<Object> points = (List<Object>) rawPoints;
        for (int i = 0; i < points.size(); i++) {
            Object rawPoint = points.get(i);
            if (!(rawPoint instanceof Map)) continue;

            Map<String, Object> point = (Map<String, Object>) rawPoint;
            Map<String, Object> modbus = map(point, "modbus");
            if (modbus == null) continue;

            String kind = str(modbus, "kind", "Holding");
            if (!"Holding".equalsIgnoreCase(kind) && !"Input".equalsIgnoreCase(kind)) continue;

            int address = integer(modbus, "address", 0);
            String niagaraAddress = str(modbus, "niagaraAddress", formatModbusAddress(kind, address));
            String dataType = str(point, "dataType", "float");

            try {
                device.setPingAddress(makeModbusAddress(niagaraAddress));
                device.setPingAddressRegType("Input".equalsIgnoreCase(kind)
                        ? BRegisterTypeEnum.input
                        : BRegisterTypeEnum.holding);
                device.setPingAddressDataType(toNiagaraDataType(dataType));
                device.setInputRegisterBaseAddress(makeModbusAddress("30001"));
                device.setHoldingRegisterBaseAddress(makeModbusAddress("40001"));
                device.setCoilStatusBaseAddress(makeModbusAddress("00001"));
                device.setInputStatusBaseAddress(makeModbusAddress("10001"));
            } catch (Exception e) {
                LOG.warning("[GridGhostImportService] configure device ping failed: " + e.getMessage());
            }
            return;
        }
    }

    private static void applyGatewaySettings(BModbusTcpGateway gateway, String host, int port) {
        try { if (host != null && !host.equals(gateway.getIpAddress())) gateway.setIpAddress(host); } catch (Exception ignore) {}
        try { if (port > 0 && gateway.getPort() != port) gateway.setPort(port); } catch (Exception ignore) {}
    }

    private static BComponent getPointsContainer(BModbusClientDevice device) {
        try {
            BValue v = device.get("points");
            if (v instanceof BComponent) return (BComponent) v;
        } catch (Exception ignore) {}
        return null;
    }

    private void importPoint(BComponent parent, Map<String, Object> point, ImportCounters counters) {
        Map<String, Object> modbus = map(point, "modbus");
        if (modbus == null) {
            counters.skipped++;
            return;
        }

        String key = str(point, "key", str(point, "name", "point"));
        String slotName = safeSlot(key, "point");
        String niagaraType = str(point, "niagaraType", "Numeric");
        String dataType = str(point, "dataType", "float");
        String access = str(point, "access", "ReadWrite");
        String kind = str(modbus, "kind", "Holding");
        int address = integer(modbus, "address", 0);
        double scale = decimal(modbus, "scale", 1.0);
        boolean boolType = isBooleanPoint(niagaraType, dataType);
        boolean writable = isWritablePoint(kind, access);
        BControlPoint controlPoint = null;
        boolean created = false;

        try {
            BValue existing = parent.get(slotName);
            if (existing instanceof BControlPoint) {
                if (isCompatiblePoint((BControlPoint) existing, boolType, writable)) {
                    controlPoint = (BControlPoint) existing;
                } else {
                    parent.remove(slotName);
                }
            } else if (existing != null) {
                counters.skipped++;
                return;
            }
        } catch (Exception ignore) {}

        try {
            if (controlPoint == null) {
                controlPoint = createPoint(boolType, writable);
                parent.add(slotName, controlPoint, Flags.SUMMARY);
                BValue mounted = parent.get(slotName);
                if (mounted instanceof BControlPoint) controlPoint = (BControlPoint) mounted;
                created = true;
            }

            configureProxy(controlPoint, niagaraType, dataType, address, kind, scale,
                    str(modbus, "niagaraAddress", ""));
            applyPointMetadata(controlPoint, point, modbus, kind, address, scale);
            if (created) counters.created++;
            else counters.updated++;
        } catch (Exception e) {
            counters.skipped++;
            LOG.warning("[GridGhostImportService] point " + key + " failed: " + e.getMessage());
        }
    }

    private static BControlPoint createPoint(boolean boolType, boolean writable) {
        if (boolType) {
            BControlPoint point = writable ? new BBooleanWritable() : new BBooleanPoint();
            point.setProxyExt(new BModbusClientBooleanProxyExt());
            return point;
        }

        BControlPoint point = writable ? new BNumericWritable() : new BNumericPoint();
        point.setProxyExt(new BModbusClientNumericProxyExt());
        return point;
    }

    private static boolean isBooleanPoint(String niagaraType, String dataType) {
        return "Boolean".equalsIgnoreCase(niagaraType) || "bool".equalsIgnoreCase(dataType);
    }

    private static boolean isWritablePoint(String kind, String access) {
        if ("Input".equalsIgnoreCase(kind) || "Discrete".equalsIgnoreCase(kind)) return false;
        return !"Read".equalsIgnoreCase(access);
    }

    private static boolean isCompatiblePoint(BControlPoint point, boolean boolType, boolean writable) {
        if (boolType) {
            if (!(point instanceof BBooleanPoint)) return false;
            return writable == (point instanceof BBooleanWritable);
        }
        if (!(point instanceof BNumericPoint)) return false;
        return writable == (point instanceof BNumericWritable);
    }

    private static void configureProxy(
            BControlPoint point,
            String niagaraType,
            String dataType,
            int address,
            String kind,
            double scale,
            String niagaraAddress) {
        try {
            BComponent proxy = (BComponent) point.getProxyExt();
            configureProxyKind(proxy, niagaraType, dataType, kind);
            configureFlexAddress(proxy, kind, address, niagaraAddress);
            trySetString(proxy, "gridGhostKind", kind);
            trySetDouble(proxy, "gridGhostScale", scale);
            trySetDouble(proxy, "scale", scale);
        } catch (Exception ignore) {}
    }

    private static void configureFlexAddress(BComponent ext, String kind, int address, String niagaraAddress) {
        try {
            String value = niagaraAddress;
            if (value == null || value.length() == 0) {
                value = formatModbusAddress(kind, address);
            }

            BFlexAddress flexAddr = null;
            try { flexAddr = flexAddress(ext, "dataAddress"); } catch (Exception ignore) {}
            if (flexAddr == null) {
                try { flexAddr = flexAddress(ext, "startingAddress"); } catch (Exception ignore) {}
            }
            if (flexAddr == null) {
                try { flexAddr = flexAddress(ext, "absoluteAddress"); } catch (Exception ignore) {}
            }
            if (flexAddr == null) flexAddr = new BFlexAddress();

            flexAddr.setAddressFormat(BAddressFormatEnum.modbus);
            flexAddr.setAddress(value);

            if (hasSlot(ext, "dataAddress")) {
                ext.set("dataAddress", flexAddr);
            } else if (hasSlot(ext, "startingAddress")) {
                ext.set("startingAddress", flexAddr);
            } else if (ext instanceof BModbusClientProxyExt) {
                ((BModbusClientProxyExt) ext).setAbsoluteAddress(flexAddr);
            } else {
                ext.set("absoluteAddress", flexAddr);
            }
        } catch (Exception e) {
            LOG.warning("[GridGhostImportService] configure address failed: " + e.getMessage());
        }
    }

    private static BFlexAddress flexAddress(BComponent component, String slotName) {
        Object value = component.get(slotName);
        return value instanceof BFlexAddress ? (BFlexAddress) value : null;
    }

    private static boolean hasSlot(BComponent component, String slotName) {
        try {
            return component.get(slotName) != null;
        } catch (Exception ignore) {
            return false;
        }
    }

    private static BFlexAddress makeModbusAddress(String address) {
        BFlexAddress flexAddr = new BFlexAddress();
        flexAddr.setAddressFormat(BAddressFormatEnum.modbus);
        flexAddr.setAddress(address);
        return flexAddr;
    }

    private static void configureProxyKind(BComponent ext, String niagaraType, String dataType, String kind) {
        try {
            if (ext instanceof BModbusClientBooleanProxyExt) {
                BModbusClientBooleanProxyExt bool = (BModbusClientBooleanProxyExt) ext;
                bool.setStatusType("Discrete".equalsIgnoreCase(kind)
                        ? BStatusTypeEnum.input
                        : BStatusTypeEnum.coil);
                return;
            }

            if (ext instanceof BModbusClientNumericProxyExt) {
                BModbusClientNumericProxyExt numeric = (BModbusClientNumericProxyExt) ext;
                numeric.setRegType("Input".equalsIgnoreCase(kind)
                        ? BRegisterTypeEnum.input
                        : BRegisterTypeEnum.holding);
                numeric.setDataType(toNiagaraDataType(dataType));
            }
        } catch (Exception e) {
            LOG.warning("[GridGhostImportService] configure proxy kind failed: " + e.getMessage());
        }
    }

    private static BDataTypeEnum toNiagaraDataType(String dataType) {
        String value = dataType == null ? "" : dataType.toLowerCase();
        if (value.indexOf("double") >= 0) return BDataTypeEnum.doubleType;
        if (value.indexOf("float") >= 0 || value.indexOf("real") >= 0) return BDataTypeEnum.floatType;
        if (value.indexOf("uint") >= 0 || value.indexOf("ulong") >= 0) return BDataTypeEnum.unsignedLong;
        if (value.indexOf("long") >= 0) return BDataTypeEnum.longType;
        if (value.indexOf("int") >= 0 || value.indexOf("short") >= 0) return BDataTypeEnum.integerType;
        return BDataTypeEnum.floatType;
    }

    private static String formatModbusAddress(String kind, int address) {
        int prefix = 4;
        if ("Coil".equalsIgnoreCase(kind)) prefix = 0;
        else if ("Discrete".equalsIgnoreCase(kind)) prefix = 1;
        else if ("Input".equalsIgnoreCase(kind)) prefix = 3;
        return String.valueOf(prefix) + pad4(address + 1);
    }

    private static String pad4(int value) {
        String s = String.valueOf(value);
        while (s.length() < 4) s = "0" + s;
        return s;
    }

    private static void applyPointMetadata(
            BControlPoint point,
            Map<String, Object> pointJson,
            Map<String, Object> modbus,
            String kind,
            int address,
            double scale) {
        putString(point, "gridGhostKey", str(pointJson, "key", ""));
        putString(point, "gridGhostName", str(pointJson, "name", ""));
        putString(point, "gridGhostNiagaraType", str(pointJson, "niagaraType", ""));
        putString(point, "gridGhostDataType", str(pointJson, "dataType", ""));
        putString(point, "gridGhostAccess", str(pointJson, "access", ""));
        putString(point, "gridGhostModbusKind", kind);
        putString(point, "gridGhostNiagaraAddress", str(modbus, "niagaraAddress", ""));
        putInt(point, "gridGhostAddress", address);
        putDouble(point, "gridGhostScale", scale);
        if (modbus.get("bitField") != null) putString(point, "gridGhostBitField", NxJson.buildObject(map(modbus, "bitField")));
        if (modbus.get("enumMapping") != null) putString(point, "gridGhostEnumMapping", modbus.get("enumMapping").toString());
    }

    private static void putString(BComponent component, String slot, String value) {
        if (value == null) value = "";
        try {
            if (component.get(slot) == null) component.add(slot, BString.make(value), Flags.SUMMARY);
            else component.set(slot, BString.make(value));
        } catch (Exception ignore) {}
    }

    private static void putInt(BComponent component, String slot, int value) {
        try {
            if (component.get(slot) == null) component.add(slot, BInteger.make(value), Flags.SUMMARY);
            else component.set(slot, BInteger.make(value));
        } catch (Exception ignore) {}
    }

    private static void putDouble(BComponent component, String slot, double value) {
        try {
            if (component.get(slot) == null) component.add(slot, BDouble.make(value), Flags.SUMMARY);
            else component.set(slot, BDouble.make(value));
        } catch (Exception ignore) {}
    }

    private static void trySetString(BComponent component, String slot, String value) {
        try {
            if (component.get(slot) != null) component.set(slot, BString.make(value));
        } catch (Exception ignore) {}
    }

    private static void trySetDouble(BComponent component, String slot, double value) {
        try {
            if (component.get(slot) != null) component.set(slot, BDouble.make(value));
        } catch (Exception ignore) {}
    }

    private static String safeSlot(String raw, String fallback) {
        String value = raw == null ? "" : raw.trim();
        if (value.length() == 0) value = fallback;
        value = value.replaceAll("[^a-zA-Z0-9_]", "_");
        if (value.length() == 0) value = fallback;
        if (Character.isDigit(value.charAt(0))) value = "_" + value;
        if (value.length() > 64) value = value.substring(0, 64);
        return value;
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> map(Map<String, Object> source, String key) {
        Object value = source.get(key);
        return value instanceof Map ? (Map<String, Object>) value : null;
    }

    private static String str(Map<String, Object> source, String key, String fallback) {
        Object value = source.get(key);
        return value == null ? fallback : value.toString();
    }

    private static int integer(Map<String, Object> source, String key, int fallback) {
        Object value = source.get(key);
        if (value instanceof Number) return ((Number) value).intValue();
        try { return value == null ? fallback : Integer.parseInt(value.toString()); }
        catch (Exception ignore) { return fallback; }
    }

    private static double decimal(Map<String, Object> source, String key, double fallback) {
        Object value = source.get(key);
        if (value instanceof Number) return ((Number) value).doubleValue();
        try { return value == null ? fallback : Double.parseDouble(value.toString()); }
        catch (Exception ignore) { return fallback; }
    }

    private static final class ImportCounters {
        int created;
        int updated;
        int skipped;
    }

    @Override
    public Type getType() { return TYPE; }
    public static final Type TYPE = Sys.loadType(BGridGhostImportService.class);

    @Override
    public BIcon getIcon() { return ICON; }
    private static final BIcon ICON = BIcon.make("module://gridGhostImporter/img/icon-ghost.svg");
}
