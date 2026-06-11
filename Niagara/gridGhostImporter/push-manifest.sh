#!/bin/bash
set -e
set -o pipefail

# push-manifest.sh - Copy GridGhost's generated manifest into station shared/.
# Usage:
#   ./push-manifest.sh [vm_password] [station_name] [manifest_path]
#
# Environment overrides:
#   VM_HOST, VM_USER, NIAGARA_USER_HOME, STATION_NAME, MANIFEST_PATH

VM_HOST="${VM_HOST:-192.168.10.87}"
VM_USER="${VM_USER:-it}"
NIAGARA_USER_HOME="${NIAGARA_USER_HOME:-/home/it/Niagara4.15/TridiumEMEA}"
DEFAULT_MANIFEST="${HOME}/Documents/GridGhost/niagara-manifest.json"

if [ -z "${1:-}" ]; then
  read -sp "VM password: " PASSWD
  echo
else
  PASSWD="$1"
fi

STATION_NAME="${2:-${STATION_NAME:-}}"
if [ -z "$STATION_NAME" ]; then
  read -rp "Station name: " STATION_NAME
fi

MANIFEST_PATH="${3:-${MANIFEST_PATH:-$DEFAULT_MANIFEST}}"
if [ ! -f "$MANIFEST_PATH" ]; then
  echo "Manifest not found: $MANIFEST_PATH"
  exit 1
fi

REMOTE_DIR="${NIAGARA_USER_HOME}/stations/${STATION_NAME}/shared"
REMOTE_FILE="${REMOTE_DIR}/gridghost-niagara-manifest.json"

echo "=== ENSURE STATION SHARED DIR ==="
sshpass -p "$PASSWD" ssh "${VM_USER}@${VM_HOST}" "mkdir -p '${REMOTE_DIR}'"

echo "=== PUSH MANIFEST ==="
sshpass -p "$PASSWD" rsync -av "$MANIFEST_PATH" "${VM_USER}@${VM_HOST}:${REMOTE_FILE}"

echo "Manifest copied to ${VM_HOST}:${REMOTE_FILE}"
echo "Niagara manifestOrd: file:^shared/gridghost-niagara-manifest.json"
