#!/usr/bin/env bash
# build_rpm.sh — turns a self-contained publish dir into an installable .rpm.
#
# Usage:
#   bash build_rpm.sh <publish-dir> [version]
#   version defaults to the one in splitwire-turkey.spec.
# Writes the .rpm next to the publish dir and leaves build tree in ./.rpmbuild.
set -euo pipefail

SRC="${1:?usage: build_rpm.sh <publish-dir> [version]}"
SRC="$(cd "$SRC" && pwd)"
NAME="splitwire-turkey"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DESKTOP="$SCRIPT_DIR/$NAME.desktop"
SPEC="$SCRIPT_DIR/$NAME.spec"

command -v rpmbuild >/dev/null 2>&1 || { echo "rpmbuild not found — run: sudo dnf install -y rpm-build"; exit 1; }

VERSION="${2:-$(sed -n 's/^Version:\s*//p' "$SPEC" | head -n1 | tr -d ' \r')}"
[[ -n "$VERSION" ]] || { echo "Cannot determine version"; exit 1; }

TOPDIR="$PWD/.rpmbuild"
rm -rf "$TOPDIR"
mkdir -p "$TOPDIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

STAGE="$TOPDIR/$NAME-$VERSION"
mkdir -p "$STAGE"
cp -r "$SRC"/. "$STAGE/"
cp "$DESKTOP" "$STAGE/$NAME.desktop"
chmod +x "$STAGE/SplitWire-Turkey-Linux" "$STAGE/install.sh" 2>/dev/null || true

tar -czf "$TOPDIR/SOURCES/$NAME-$VERSION.tar.gz" -C "$TOPDIR" "$NAME-$VERSION"
cp "$SPEC" "$TOPDIR/SPECS/"

rpmbuild -bb --define "_topdir $TOPDIR" "$TOPDIR/SPECS/$NAME.spec"

OUT="$(pwd)"
cp "$TOPDIR"/RPMS/*/*.rpm "$OUT/"
RPM_PATH="$(ls -1 "$OUT"/$NAME-$VERSION-*.rpm | tail -n1)"
rm -rf "$TOPDIR"
echo "RPM: $RPM_PATH"
