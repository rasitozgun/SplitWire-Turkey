# SplitWire-Turkey — Linux Edition

> **Status: tested and working on Fedora 44 (x86_64, Hyper-V VM, GNOME/Wayland).**
> The Windows edition stays untouched. This document explains what the Linux port
> contains (milestone 2) and how the existing Windows app can now be compiled on
> Fedora for CI purposes (milestone 1).
>
> Tested end-to-end (commits up to `749e2b7`): GUI launches on both X11 and
> XWayland, Cloudflare WARP tunnel via `wgcf`/`wg-quick` (verified with
> `curl https://www.cloudflare.com/cdn-cgi/trace` → `warp=on`), ByeDPI (`ciadpi`)
> and Zapret (`tpws`) downloads + systemd service installs, and the self-contained
> release tarball at <https://github.com/rasitozgun/SplitWire-Turkey/releases/tag/linux>.

---

## What works on Linux today

| Windows feature | Linux port status |
|---|---|
| WireSock (Cloudflare WARP split tunnel) | ✅ `wgcf` + `wg-quick` system tunnel (no per-app split tunneling — WireSock is Windows-only) |
| ByeDPI split tunneling | ⚠️ `ciadpi` runs as systemd service; point your browser/app at `127.0.0.1:1080` (Linux has no DLL-injection `drover`) |
| Zapret | ✅ `tpws` from the official Linux binaries as a systemd service + `blockcheck.sh` runner |
| GoodbyeDPI | ❌ Not available (WinDivert is Windows-only) — use zapret |
| DNS change / reset | ✅ via NetworkManager (`nmcli`) on Fedora |
| Services overview / remove all | ✅ systemd |

## Build on Fedora

```bash
sudo dnf install dotnet-sdk-8.0 tar gzip
cd src
./build_fedora.sh --self-contained
```

Framework-dependent build: `src/SplitWireTurkey.Linux/bin/Release/net8.0/SplitWire-Turkey-Linux`

## Run

```bash
src/SplitWireTurkey.Linux/bin/Release/net8.0/SplitWire-Turkey-Linux
```

Requirements at runtime: `systemd`, NetworkManager (`nmcli`, Fedora default),
`pkexec` (or `sudo`), `wg-quick` + `wireguard-tools` for the tunnel tab
(`sudo dnf install wireguard-tools`), `tar`/`gzip`.
Tunnel/service actions prompt for root through `pkexec`.

## Compile-checking the Windows WPF app on Fedora

Since .NET 7, `net8.0-windows` WPF projects can be *built* on Linux with
`EnableWindowsTargeting` (they still only run on Windows):

```bash
dotnet build src/SplitWireTurkey -c Release -p:EnableWindowsTargeting=true
```

CI (GitHub Actions, `.github/workflows/fedora.yml`) runs both jobs on a real
Fedora container: Linux edition build/publish + WPF cross-compile check.

## Settings and logs

- Config: `~/.config/SplitWire-Turkey/settings.json` (replaces Windows registry)
- Logs: `~/.local/share/SplitWire-Turkey/Logs/*.log`
- Languages: the exact same `res/Languages/*.json` files as the Windows edition

## Roadmap

1. Per-ISP preset tuning for tpws on Turkish ISPs (community feedback needed; the
   bundled `res/zapret/presets-linux.json` is a starting point).
2. WireGuard profile helper for split-tunneling via nft rules.
3. DNS-over-HTTPS option via dnscrypt-proxy system package.
4. Localization of the Linux-specific UI strings (tr/en/ru/es).
5. Flatpak packaging + .desktop entry + icon.
