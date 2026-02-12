# Build Script
$ErrorActionPreference = "Stop"

Write-Host "Restoring dependencies..."
dotnet restore

Write-Host "Building solution..."
dotnet build --configuration Release

Write-Host "Publishing DeviceSim.App..."
dotnet publish DeviceSim.App/DeviceSim.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -o Publish

Write-Host "Build complete. Executable is in Publish/DeviceSim.App.exe"
