#!/usr/bin/env bash
# ============================================================
# GridGhost v2.1.0 — Linux publish + AppImage packager
# Run this script ON LINUX after cloning the repo.
# Requires: dotnet 8 SDK, appimagetool (in PATH or ./tools/)
# ============================================================
set -euo pipefail

VERSION="2.1.0"
APP_NAME="GridGhost"
PUBLISH_DIR="DeviceSim.App/Publish-linux"
APPDIR="AppDir"

echo "==> [1/5] Restoring & publishing (linux-x64, self-contained)..."
dotnet publish DeviceSim.App/DeviceSim.App.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:StripSymbols=true \
    -o "$PUBLISH_DIR"

echo "==> [2/5] Copying Templates..."
cp -r Templates "$PUBLISH_DIR/Templates"

echo "==> [3/5] Building AppDir structure..."
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

# Binary + data
cp -r "$PUBLISH_DIR/"* "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/DeviceSim.App"

# Icon (AppImage needs a PNG at root too)
cp DeviceSim.App/Assets/Icons/gridghost_icon.png "$APPDIR/usr/share/icons/hicolor/256x256/apps/gridghost.png"
cp DeviceSim.App/Assets/Icons/gridghost_icon.png "$APPDIR/gridghost.png"

# .desktop file
cat > "$APPDIR/usr/share/applications/gridghost.desktop" <<EOF
[Desktop Entry]
Name=GridGhost
Comment=Modbus Device Simulator
Exec=DeviceSim.App
Icon=gridghost
Type=Application
Categories=Utility;Network;
StartupWMClass=DeviceSim.App
EOF

# AppImage also needs .desktop at root and AppRun entrypoint
cp "$APPDIR/usr/share/applications/gridghost.desktop" "$APPDIR/gridghost.desktop"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
export PATH="$HERE/usr/bin:$PATH"
cd "$HERE/usr/bin"
exec "$HERE/usr/bin/DeviceSim.App" "$@"
EOF
chmod +x "$APPDIR/AppRun"

echo "==> [4/5] Running appimagetool..."
# Look for appimagetool in PATH, then ./tools/
TOOL=""
if command -v appimagetool &>/dev/null; then
    TOOL="appimagetool"
elif [ -f "./tools/appimagetool-x86_64.AppImage" ]; then
    TOOL="./tools/appimagetool-x86_64.AppImage"
else
    echo ""
    echo "ERROR: appimagetool not found."
    echo "Download it from: https://github.com/AppImage/AppImageKit/releases"
    echo "Place it in ./tools/appimagetool-x86_64.AppImage  OR  add it to PATH"
    exit 1
fi

ARCH=x86_64 "$TOOL" "$APPDIR" "GridGhost-${VERSION}-x86_64.AppImage"

echo ""
echo "==> [5/5] Done!"
echo "    Output: GridGhost-${VERSION}-x86_64.AppImage"
