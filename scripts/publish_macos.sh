#!/bin/bash
set -e

echo "Publishing GridGhost for macOS..."

# Determine architectures (osx-arm64 for Apple Silicon, osx-x64 for Intel)
ARCHS=("osx-arm64" "osx-x64")

for ARCH in "${ARCHS[@]}"; do
    echo "----------------------------------------"
    echo "Building executable for $ARCH..."
    echo "----------------------------------------"
    
    # 1. Publish standalone single-file binary
    dotnet publish DeviceSim/DeviceSim.App/DeviceSim.App.csproj \
      -c Release \
      -r "$ARCH" \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -o "./publish/$ARCH"
      
    # 2. Build native macOS .app bundle directory structure
    APP_DIR="./publish/${ARCH}-app/GridGhost.app"
    echo "Structuring macOS App Bundle: $APP_DIR"
    
    mkdir -p "$APP_DIR/Contents/MacOS"
    mkdir -p "$APP_DIR/Contents/Resources"
    
    # Copy the compiled binary
    cp "./publish/$ARCH/GridGhost" "$APP_DIR/Contents/MacOS/"
    
    # Copy other assets if needed (e.g. templates)
    if [ -d "./publish/$ARCH/Templates" ]; then
        cp -R "./publish/$ARCH/Templates" "$APP_DIR/Contents/MacOS/"
    fi
    
    # 3. Create Info.plist with app metadata
    cat <<EOF > "$APP_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>GridGhost</string>
    <key>CFBundleIdentifier</key>
    <string>com.zehmsy.gridghost</string>
    <key>CFBundleName</key>
    <string>GridGhost</string>
    <key>CFBundleVersion</key>
    <string>2.1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>2.1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>gridghost_icon.icns</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

    # 4. Generate native macOS .icns file from PNG using sips and iconutil
    PNG_ICON="DeviceSim/DeviceSim.App/Assets/Icons/gridghost_icon.png"
    if [ -f "$PNG_ICON" ]; then
        echo "Creating native macOS icon (.icns) from PNG..."
        mkdir -p temp.iconset
        
        sips -z 16 16     "$PNG_ICON" --out temp.iconset/icon_16x16.png > /dev/null 2>&1
        sips -z 32 32     "$PNG_ICON" --out temp.iconset/icon_16x16@2x.png > /dev/null 2>&1
        sips -z 32 32     "$PNG_ICON" --out temp.iconset/icon_32x32.png > /dev/null 2>&1
        sips -z 64 64     "$PNG_ICON" --out temp.iconset/icon_32x32@2x.png > /dev/null 2>&1
        sips -z 128 128   "$PNG_ICON" --out temp.iconset/icon_128x128.png > /dev/null 2>&1
        sips -z 256 256   "$PNG_ICON" --out temp.iconset/icon_128x128@2x.png > /dev/null 2>&1
        sips -z 256 256   "$PNG_ICON" --out temp.iconset/icon_256x256.png > /dev/null 2>&1
        sips -z 512 512   "$PNG_ICON" --out temp.iconset/icon_256x256@2x.png > /dev/null 2>&1
        sips -z 512 512   "$PNG_ICON" --out temp.iconset/icon_512x512.png > /dev/null 2>&1
        sips -z 1024 1024 "$PNG_ICON" --out temp.iconset/icon_512x512@2x.png > /dev/null 2>&1
        
        iconutil -c icns temp.iconset -o "$APP_DIR/Contents/Resources/gridghost_icon.icns"
        rm -rf temp.iconset
        echo "Icon successfully generated."
    else
        echo "Warning: PNG icon not found. Skipping .icns creation."
    fi
done

echo "----------------------------------------"
echo "Success! macOS app bundles created:"
echo "  - ./publish/osx-arm64-app/GridGhost.app"
echo "  - ./publish/osx-x64-app/GridGhost.app"
echo "----------------------------------------"
