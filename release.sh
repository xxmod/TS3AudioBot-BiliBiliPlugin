#!/bin/bash

echo "Building TS3AudioBot BiliBili Plugin Release..."
echo

echo "[1/3] Cleaning previous build..."
dotnet clean --configuration Release

echo "[2/3] Building release..."
dotnet build --configuration Release --no-restore

echo "[3/3] Verifying release files..."
if [ -f "bin/Release/netcoreapp3.1/BilibiliPlugin.dll" ]; then
    echo "✓ BilibiliPlugin.dll found"
else
    echo "✗ BilibiliPlugin.dll missing!"
    exit 1
fi

if [ -f "bin/Release/netcoreapp3.1/Newtonsoft.Json.dll" ]; then
    echo "✓ Newtonsoft.Json.dll found"
else
    echo "✗ Newtonsoft.Json.dll missing!"
    exit 1
fi

echo
echo "Release build completed successfully!"
echo "Output directory: bin/Release/netcoreapp3.1/"
echo
echo "Required files for TS3AudioBot:"
echo "- BilibiliPlugin.dll"
echo "- Newtonsoft.Json.dll"
echo