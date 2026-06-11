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

echo "Done! Artifacts are in ./publish/"
