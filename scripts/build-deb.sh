#!/usr/bin/env bash
# Debian/Ubuntu (.deb) Packaging Script for PocketMC
# Usage: ./scripts/build-deb.sh <rid> <version> [publish_dir]

set -e

RID="${1:-linux-x64}"
VERSION="${2:-1.0.0}"
PUBLISH_DIR="${3:-publish}"
DEB_ARCH="amd64"

if [ "${RID}" = "linux-arm64" ]; then
  DEB_ARCH="arm64"
fi

OUTPUT_DEB="pocketmc_${VERSION}_${DEB_ARCH}.deb"

echo "==> Packaging Debian package for ${RID} (${DEB_ARCH}, v${VERSION})..."

if [ ! -d "${PUBLISH_DIR}" ]; then
  echo "Error: Publish directory '${PUBLISH_DIR}' does not exist."
  exit 1
fi

DEB_ROOT=$(mktemp -d /tmp/pocketmc-deb.XXXXXX)
trap 'rm -rf "${DEB_ROOT}"' EXIT

mkdir -p "${DEB_ROOT}/DEBIAN"
mkdir -p "${DEB_ROOT}/usr/lib/pocketmc"
mkdir -p "${DEB_ROOT}/usr/bin"
mkdir -p "${DEB_ROOT}/usr/share/applications"
mkdir -p "${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps"

# Copy binary payload
cp -R "${PUBLISH_DIR}/"* "${DEB_ROOT}/usr/lib/pocketmc/"
chmod +x "${DEB_ROOT}/usr/lib/pocketmc/PocketMC.App"

# Create symlink launcher in /usr/bin
ln -sf /usr/lib/pocketmc/PocketMC.App "${DEB_ROOT}/usr/bin/pocketmc"

# Copy desktop and icon assets
cp "scripts/pocketmc.desktop" "${DEB_ROOT}/usr/share/applications/pocketmc.desktop"
if [ -f "PocketMC.App/Assets/logo_highres.png" ]; then
  cp "PocketMC.App/Assets/logo_highres.png" "${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps/pocketmc.png"
elif [ -f "PocketMC.App/Assets/icon.png" ]; then
  cp "PocketMC.App/Assets/icon.png" "${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps/pocketmc.png"
fi

# Generate DEBIAN/control
cat << EOF > "${DEB_ROOT}/DEBIAN/control"
Package: pocketmc
Version: ${VERSION}
Section: games
Priority: optional
Architecture: ${DEB_ARCH}
Depends: libc6 (>= 2.27), libgcc-s1, libicu-dev | libicu74 | libicu72 | libicu70 | libicu66, libssl-dev | libssl3 | libssl1.1
Maintainer: DS Labs <contact@pocketmc.net>
Description: PocketMC Desktop Server Manager
 A modern native desktop application for managing Minecraft Java,
 Bedrock Dedicated Server, and PocketMine (PHP) servers with unified process
 monitoring and playit.gg cross-platform network tunnels.
EOF

if command -v dpkg-deb >/dev/null 2>&1; then
  dpkg-deb --build --root-owner-group "${DEB_ROOT}" "${OUTPUT_DEB}"
  echo "[SUCCESS] Generated ${OUTPUT_DEB}"
else
  echo "Notice: 'dpkg-deb' not found. Staged debian layout at ${DEB_ROOT}."
fi
