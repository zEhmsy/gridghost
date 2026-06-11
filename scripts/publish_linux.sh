#!/bin/bash
set -e

echo "Publishing GridGhost for Linux..."

# Linux x64
echo "Building linux-x64 (Framework Dependent)..."
dotnet publish DeviceSim/DeviceSim.App/DeviceSim.App.csproj -c Release -r linux-x64 --self-contained false -o ./publish/linux-x64

echo "Building linux-x64 (Self Contained - Single File)..."
dotnet publish DeviceSim/DeviceSim.App/DeviceSim.App.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/linux-x64-sc

# Linux ARM64 (Optional)
echo "Building linux-arm64 (Self Contained)..."
dotnet publish DeviceSim/DeviceSim.App/DeviceSim.App.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/linux-arm64-sc

# --- AppImage Packaging Preparation ---
APPDIR="./publish/GridGhost.AppDir"
echo "Structuring Linux AppDir: $APPDIR"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

# Copy binary and templates
cp "./publish/linux-x64-sc/GridGhost" "$APPDIR/usr/bin/"
if [ -d "./publish/linux-x64-sc/Templates" ]; then
    cp -R "./publish/linux-x64-sc/Templates" "$APPDIR/usr/bin/"
fi

# Copy desktop entry and icon
cp "DeviceSim/DeviceSim.App/Assets/Icons/gridghost.desktop" "$APPDIR/"
cp "DeviceSim/DeviceSim.App/Assets/Icons/gridghost_icon.png" "$APPDIR/gridghost_icon.png"
cp "DeviceSim/DeviceSim.App/Assets/Icons/gridghost_icon.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/gridghost_icon.png"

# Create AppRun entrypoint
cat <<'EOF' > "$APPDIR/AppRun"
#!/bin/sh
SELF=$(readlink -f "$0")
HERE=$(dirname "$SELF")
export PATH="${HERE}/usr/bin:${PATH}"
exec "${HERE}/usr/bin/GridGhost" "$@"
EOF
chmod +x "$APPDIR/AppRun"

echo "----------------------------------------"
if command -v appimagetool >/dev/null 2>&1; then
    echo "appimagetool found! Packaging AppImage..."
    ARCH=x86_64 appimagetool "$APPDIR" "./publish/GridGhost-3.0.0-x86_64.AppImage"
    echo "AppImage created: ./publish/GridGhost-3.0.0-x86_64.AppImage"
else
    echo "AppDir structured successfully at $APPDIR"
    echo "To package it as an AppImage on a Linux machine (or CI), run:"
    echo "  ARCH=x86_64 appimagetool ./publish/GridGhost.AppDir ./publish/GridGhost-3.0.0-x86_64.AppImage"
fi
echo "----------------------------------------"

echo "Done! Artifacts are in ./publish/"
