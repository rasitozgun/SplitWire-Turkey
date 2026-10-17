#!/usr/bin/env bash
# build_fedora.sh — build SplitWire-Turkey Linux edition on Fedora.
#
# Usage:
#   ./build_fedora.sh                build (framework-dependent)
#   ./build_fedora.sh --self-contained     also publish a portable linux-x64 build
#
# Prerequisites (run once):
#   sudo dnf install dotnet-sdk-8.0 tar gzip
#   optional desktop tools at runtime: nmcli (NetworkManager), systemd, pkexec

set -euo pipefail

cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: .NET SDK not found. Install with:  sudo dnf install dotnet-sdk-8.0"
    exit 1
fi

APP_DIR="SplitWireTurkey.Linux"

echo "==> Building SplitWire-Turkey (Linux edition) ..."
dotnet build "$APP_DIR" -c Release

echo "==> Verifying language resources were staged ..."
if [ ! -f "$APP_DIR/bin/Release/net8.0/res/Languages/en.json" ]; then
    echo "WARNING: en.json missing in output res/Languages - copying manually."
    install -d "$APP_DIR/bin/Release/net8.0/res/Languages"
    cp SplitWireTurkey/Resources/Languages/*.json "$APP_DIR/bin/Release/net8.0/res/Languages/" 2>/dev/null || true
fi

echo
echo "Build OK."
echo "Run it:  $APP_DIR/bin/Release/net8.0/SplitWire-Turkey-Linux"
echo

if [[ "${1:-}" == "--self-contained" ]]; then
    echo "==> Publishing self-contained linux-x64 build ..."
    dotnet publish "$APP_DIR" -c Release -r linux-x64 --self-contained true \
        -o "$APP_DIR/publish/linux-x64"
    install -d "$APP_DIR/publish/linux-x64/res/Languages"
    cp SplitWireTurkey/Resources/Languages/*.json "$APP_DIR/publish/linux-x64/res/Languages/" 2>/dev/null || true
    find "$APP_DIR/publish/linux-x64/res" -maxdepth 3 -type f | sort
    echo "Publish OK: $APP_DIR/publish/linux-x64/SplitWire-Turkey-Linux"
    echo "Portable tar: (optional)  tar -czf splitwire-turkey-linux-x64.tar.gz -C $APP_DIR/publish linux-x64"
fi
