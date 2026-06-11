#!/bin/bash
set -e
set -o pipefail

# deploy.sh - Sync, build, sign, and deploy gridGhostImporter to the Niagara VM.
# Usage:
#   SIGN_PASS='...' ./deploy.sh [vm_password]

VM_HOST="${VM_HOST:-192.168.10.87}"
VM_USER="${VM_USER:-it}"
VM_WORKSPACE="${VM_WORKSPACE:-/home/it/Scrivania/gridGhostImporter}"
NIAGARA_HOME="${NIAGARA_HOME:-/opt/Niagara/Niagara-4.15.3.28}"
NIAGARA_MODULES="${NIAGARA_MODULES:-${NIAGARA_HOME}/modules}"
WB_REGISTRY="${WB_REGISTRY:-/home/it/Niagara4.15/TridiumEMEA/registry}"

MODULE_NAME="gridGhostImporter"
MODULE_RT_JAR="gridGhostImporter-rt/build/libs/gridGhostImporter-rt.jar"
MODULE_WB_JAR="gridGhostImporter-wb/build/libs/gridGhostImporter-wb.jar"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SIGN_P12="${SIGN_P12:-${SCRIPT_DIR}/code.p12}"
SIGN_ALIAS="${SIGN_ALIAS:-code}"
VM_P12="${VM_P12:-/home/it/code.p12}"

if [ -z "${SIGN_PASS:-}" ]; then
  read -sp "Keystore password: " SIGN_PASS
  echo
fi

if [ ! -f "$SIGN_P12" ]; then
  echo "Missing keystore: $SIGN_P12"
  echo "Place code.p12 in ${SCRIPT_DIR} or set SIGN_P12."
  exit 1
fi

if [ -z "${1:-}" ]; then
  read -sp "VM password: " PASSWD
  echo
else
  PASSWD="$1"
fi

echo "=== SYNC to VM ==="
sshpass -p "$PASSWD" rsync -av \
  --delete \
  --exclude='build/' \
  --exclude='.gradle/' \
  --exclude='.DS_Store' \
  --exclude='*.p12' \
  "${SCRIPT_DIR}/" "${VM_USER}@${VM_HOST}:${VM_WORKSPACE}/" | grep -E "\.java|\.xml|\.gradle|\.kts|\.sh|sent" || true

echo "=== SYNC KEYSTORE ==="
sshpass -p "$PASSWD" rsync -av "$SIGN_P12" "${VM_USER}@${VM_HOST}:${VM_P12}"

echo "=== BUILD on VM ==="
sshpass -p "$PASSWD" ssh "${VM_USER}@${VM_HOST}" \
  "cd ${VM_WORKSPACE} && chmod +x ./gradlew && ./gradlew clean jar --parallel 2>&1"

echo "=== SIGN JARs ==="
sshpass -p "$PASSWD" ssh "${VM_USER}@${VM_HOST}" bash << ENDSSH
set -e
for JAR in "${VM_WORKSPACE}/${MODULE_RT_JAR}" "${VM_WORKSPACE}/${MODULE_WB_JAR}"; do
  echo "  Signing: \$(basename "\$JAR")"
  jarsigner \
    -keystore "${VM_P12}" \
    -storetype PKCS12 \
    -storepass '${SIGN_PASS}' \
    -keypass '${SIGN_PASS}' \
    -tsa http://timestamp.digicert.com \
    "\$JAR" "${SIGN_ALIAS}"
  jarsigner -verify "\$JAR" 2>&1 | grep -E "verified|Warning" | head -2
done
ENDSSH

echo "=== DEPLOY to Niagara ==="
sshpass -p "$PASSWD" ssh "${VM_USER}@${VM_HOST}" "
  echo '$PASSWD' | sudo -S cp ${VM_WORKSPACE}/${MODULE_RT_JAR} ${NIAGARA_MODULES}/ &&
  echo '$PASSWD' | sudo -S cp ${VM_WORKSPACE}/${MODULE_WB_JAR} ${NIAGARA_MODULES}/ &&
  echo '$PASSWD' | sudo -S systemctl restart n4d.service &&
  sleep 5 &&
  echo '$PASSWD' | sudo -S systemctl is-active n4d.service &&
  echo 'Deploy OK'
"

echo "=== WORKBENCH REGISTRY CACHE ==="
sshpass -p "$PASSWD" ssh "${VM_USER}@${VM_HOST}" "
  python3 - <<'PY'
from pathlib import Path
import xml.etree.ElementTree as ET

module = '${MODULE_NAME}'
path = Path('${WB_REGISTRY}/../etc/options/paletteSideBar.options').resolve()
if path.exists():
    tree = ET.parse(path)
    root = tree.getroot()
    options = root.find('p')
    if options is not None:
        existing = {p.attrib.get('v') for p in options.findall('p')}
        if module not in existing:
            max_idx = -1
            for p in options.findall('p'):
                name = p.attrib.get('n', '')
                if name == 'String':
                    max_idx = max(max_idx, 0)
                elif name.startswith('String') and name[6:].isdigit():
                    max_idx = max(max_idx, int(name[6:]))
            ET.SubElement(options, 'p', {'n': f'String{max_idx + 1}', 't': 'b:String', 'v': module})
            tree.write(path, encoding='UTF-8', xml_declaration=True)
            print(f'Added {module} to Workbench palette sidebar options.')
        else:
            print(f'{module} already present in Workbench palette sidebar options.')
else:
    print(f'Palette sidebar options not found: {path}')
PY
  if pgrep -f '[b]in/wb' >/dev/null; then
    echo 'Workbench is running: close it completely and reopen it to reload module palettes.'
  else
    rm -f ${WB_REGISTRY}/registry.db ${WB_REGISTRY}/registry.chk
    ${NIAGARA_HOME}/bin/nre -rp:rt,se,ux,wb -buildreg >/tmp/gridghostimporter-buildreg.log 2>&1 || {
      cat /tmp/gridghostimporter-buildreg.log
      exit 1
    }
    grep -E 'Rebuilt:|gridGhostImporter|Force rebuild' /tmp/gridghostimporter-buildreg.log || true
    echo 'Workbench registry cache rebuilt.'
  fi
"

echo "=== COMPLETE ==="
