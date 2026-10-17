Name:           splitwire-turkey
Version:        1.5.6
Release:        2%{?dist}
Summary:        Various restriction bypass tools - Linux edition (self-contained GUI)
License:        MIT
URL:            https://github.com/rasitozgun/SplitWire-Turkey
BuildArch:      x86_64
Source0:        %{name}-%{version}.tar.gz
Requires:       wireguard-tools

# The app is self-contained (.NET 8 runtime bundled): the dependency scanner
# would generate nonsense deps from the bundled shared libraries, so disable it.
AutoReqProv:    no
# Self-contained prebuilt app: no debuginfo/debugsource packages possible.
%define debug_package %{nil}

%description
SplitWire-Turkey (Linux edition) is an Avalonia 11 / .NET 8 GUI that automates
Cloudflare WARP registration (wgcf + wg-quick), ByeDPI (ciadpi) and Zapret
(tpws) as systemd services. Fully self-contained: ships its own .NET runtime,
no SDK needed.

%prep
%setup -q -n %{name}-%{version}

%install
rm -rf %{buildroot}
install -d %{buildroot}/opt/%{name}
cp -p -r ./* %{buildroot}/opt/%{name}/
install -d %{buildroot}%{_datadir}/applications
install -m 644 %{name}.desktop %{buildroot}%{_datadir}/applications/%{name}.desktop
chmod +x %{buildroot}/opt/%{name}/SplitWire-Turkey-Linux
chmod +x %{buildroot}/opt/%{name}/install.sh 2>/dev/null || true

%files
/opt/%{name}
%{_datadir}/applications/%{name}.desktop

%changelog
* Sat Oct 18 2026 rasitozgun <rasitozgun@users.noreply.github.com> - 1.5.6-2
- Invariant globalization mode: fixes startup crash on distros with newer ICU (77+) (rh#CentOS/Fedora).
* Fri Oct 17 2026 rasitozgun <rasitozgun@users.noreply.github.com> - 1.5.6-1
- First RPM build of the Linux edition (WARP, ByeDPI, Zapret).
