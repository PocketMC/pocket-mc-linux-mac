#!/usr/bin/env bash
# RPM Packaging Script for PocketMC (Fedora, RHEL, CentOS)
# Usage: ./scripts/build-rpm.sh <rid> <version> [publish_dir]

set -e

RID="${1:-linux-x64}"
VERSION="${2:-1.0.0}"
PUBLISH_DIR="${3:-publish}"
RPM_ARCH="x86_64"

if [ "${RID}" = "linux-arm64" ]; then
  RPM_ARCH="aarch64"
fi

echo "==> Packaging RPM package for ${RID} (${RPM_ARCH}, v${VERSION})..."

if [ ! -d "${PUBLISH_DIR}" ]; then
  echo "Error: Publish directory '${PUBLISH_DIR}' does not exist."
  exit 1
fi

RPM_ROOT=$(mktemp -d /tmp/pocketmc-rpm.XXXXXX)
trap 'rm -rf "${RPM_ROOT}"' EXIT

mkdir -p "${RPM_ROOT}/BUILD"
mkdir -p "${RPM_ROOT}/RPMS"
mkdir -p "${RPM_ROOT}/SOURCES"
mkdir -p "${RPM_ROOT}/SPECS"
mkdir -p "${RPM_ROOT}/SRPMS"

cat << EOF > "${RPM_ROOT}/SPECS/pocketmc.spec"
Name:           pocketmc
Version:        ${VERSION}
Release:        1%{?dist}
Summary:        PocketMC Desktop Minecraft Server Manager
License:        MIT
URL:            https://pocketmc.net
BuildArch:      ${RPM_ARCH}

%description
A modern native desktop application for managing Minecraft Java,
Bedrock Dedicated Server, and PocketMine (PHP) servers with unified process
monitoring and playit.gg cross-platform network tunnels.

%install
mkdir -p %{buildroot}/usr/lib/pocketmc
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps

cp -R ${PWD}/${PUBLISH_DIR}/* %{buildroot}/usr/lib/pocketmc/
chmod +x %{buildroot}/usr/lib/pocketmc/PocketMC.App
ln -sf /usr/lib/pocketmc/PocketMC.App %{buildroot}/usr/bin/pocketmc
cp ${PWD}/scripts/pocketmc.desktop %{buildroot}/usr/share/applications/pocketmc.desktop
if [ -f "${PWD}/PocketMC.App/Assets/logo_highres.png" ]; then
  cp "${PWD}/PocketMC.App/Assets/logo_highres.png" %{buildroot}/usr/share/icons/hicolor/256x256/apps/pocketmc.png
fi

%files
/usr/lib/pocketmc/*
/usr/bin/pocketmc
/usr/share/applications/pocketmc.desktop
/usr/share/icons/hicolor/256x256/apps/pocketmc.png
EOF

if command -v rpmbuild >/dev/null 2>&1; then
  rpmbuild --define "_topdir ${RPM_ROOT}" -bb "${RPM_ROOT}/SPECS/pocketmc.spec"
  cp "${RPM_ROOT}/RPMS/${RPM_ARCH}/"* .
  echo "[SUCCESS] Generated RPM packages."
else
  echo "Notice: 'rpmbuild' not found. RPM spec file generated at ${RPM_ROOT}/SPECS/pocketmc.spec."
fi
