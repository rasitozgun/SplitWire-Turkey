#!/usr/bin/env bash
# package_linux.sh — stages + tars the self-contained linux-x64 build into the
# release artifact "SplitWire-Turkey-Linux-x64.tar.gz".
#
# Expects the publish output directory as $1 (default: publish/linux-x64)
# and writes the tarball next to it.
set -euo pipefail

PUBLISH_DIR="${1:-publish/linux-x64}"
OUT="${2:-SplitWire-Turkey-Linux-x64.tar.gz}"

[[ -d "$PUBLISH_DIR" ]] || { echo "Publish dir not found: $PUBLISH_DIR"; exit 1; }

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

mkdir -p "$STAGE/splitwire-turkey-linux-x64"
cp -r "$PUBLISH_DIR"/. "$STAGE/splitwire-turkey-linux-x64/"

# Installer + packaging docs go inside the tarball
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cp "$SCRIPT_DIR/install.sh" "$STAGE/splitwire-turkey-linux-x64/"
chmod +x "$STAGE/splitwire-turkey-linux-x64/install.sh"

# Executable bit for the app binary + any shipped scripts
chmod +x "$STAGE/splitwire-turkey-linux-x64/SplitWire-Turkey-Linux" || true

tar -czf "$OUT" -C "$STAGE" splitwire-turkey-linux-x64
echo "Packaged: $(pwd)/$OUT"
