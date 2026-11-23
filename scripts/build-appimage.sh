#!/usr/bin/env bash
# Linux AppImage Packaging Script for PocketMC
# Usage: ./scripts/build-appimage.sh <rid> <version> [publish_dir]

set -e

RID="${1:-linux-x64}"
VERSION="${2:-1.0.0}"
PUBLISH_DIR="${3:-publish}"
ARCH="x86_64"

if [ "${RID}" = "linux-arm64" ]; then
  ARCH="aarch64"
fi

OUTPUT_APPIMAGE="PocketMC-${VERSION}-${ARCH}.AppImage"

echo "==> Packaging Linux AppImage for ${RID} (${ARCH}, v${VERSION})..."

if [ ! -d "${PUBLISH_DIR}" ]; then
  echo "Error: Publish directory '${PUBLISH_DIR}' does not exist."
  exit 1
fi

APP_DIR=$(mktemp -d /tmp/pocketmc-appdir.XXXXXX)
trap 'rm -rf "${APP_DIR}"' EXIT

mkdir -p "${APP_DIR}/usr/bin"
mkdir -p "${APP_DIR}/usr/share/icons/hicolor/256x256/apps"
mkdir -p "${APP_DIR}/usr/share/applications"

# Copy published application files
cp -R "${PUBLISH_DIR}/"* "${APP_DIR}/usr/bin/"
chmod +x "${APP_DIR}/usr/bin/PocketMC.App"

# Copy metadata and desktop entry
cp "scripts/pocketmc.desktop" "${APP_DIR}/pocketmc.desktop"
cp "scripts/pocketmc.desktop" "${APP_DIR}/usr/share/applications/pocketmc.desktop"

# Copy icons
if [ -f "PocketMC.App/Assets/logo_highres.png" ]; then
  cp "PocketMC.App/Assets/logo_highres.png" "${APP_DIR}/pocketmc.png"
  cp "PocketMC.App/Assets/logo_highres.png" "${APP_DIR}/usr/share/icons/hicolor/256x256/apps/pocketmc.png"
elif [ -f "PocketMC.App/Assets/icon.png" ]; then
  cp "PocketMC.App/Assets/icon.png" "${APP_DIR}/pocketmc.png"
  cp "PocketMC.App/Assets/icon.png" "${APP_DIR}/usr/share/icons/hicolor/256x256/apps/pocketmc.png"
fi

# Create AppRun entrypoint
cat << 'EOF' > "${APP_DIR}/AppRun"
#!/bin/sh
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/PocketMC.App" "$@"
EOF
chmod +x "${APP_DIR}/AppRun"

if command -v appimagetool >/dev/null 2>&1; then
  ARCH="${ARCH}" appimagetool "${APP_DIR}" "${OUTPUT_APPIMAGE}"
  echo "[SUCCESS] Generated ${OUTPUT_APPIMAGE}"
else
  echo "Notice: 'appimagetool' not found in path. AppDir bundle staged at ${APP_DIR}."
fi
