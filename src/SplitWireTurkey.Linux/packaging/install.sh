#!/usr/bin/env bash
# install.sh — installs the portable SplitWire-Turkey Linux build system-wide.
#
# Usage (from the extracted release folder):
#   sudo ./install.sh          install to /opt, add launcher + desktop icon
#   sudo ./install.sh --uninstall   remove everything again
set -euo pipefail

INSTALL_DIR="/opt/splitwire-turkey"
BIN_NAME="SplitWire-Turkey-Linux"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"

if [[ "${1:-}" == "--uninstall" ]]; then
    if command -v pkexec >/dev/null 2>&1; then
        pkexec rm -rf "$INSTALL_DIR"
        pkexec rm -f /usr/share/applications/splitwire-turkey.desktop
    else
        echo "Root needed to uninstall (pkexec not available)."
        exit 1
    fi
    echo "SplitWire-Turkey (Linux) uninstalled."
    exit 0
fi

if [[ $EUID -ne 0 ]]; then
    echo "Run with sudo or pkexec:   sudo ./install.sh"
    exit 1
fi

echo "==> Installing SplitWire-Turkey (Linux edition) to $INSTALL_DIR ..."
mkdir -p "$INSTALL_DIR"
cp -r ./* "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/$BIN_NAME"

echo "==> Creating desktop launcher ..."
cat > "$DESKTOP_DIR/splitwire-turkey.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=SplitWire-Turkey
GenericName=DPI Bypass and Tunneling Automation
Comment=Various restriction bypass tools (Linux edition)
Exec=$INSTALL_DIR/$BIN_NAME
Icon=utilities-terminal
Terminal=false
Categories=Network;
EOF

echo
echo "Done. Start it from your applications menu (SplitWire-Turkey)"
echo "or run:  $INSTALL_DIR/$BIN_NAME"
echo
echo "To remove later:   sudo ./install.sh --uninstall"
