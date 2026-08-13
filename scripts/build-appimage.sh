#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${ROOT_DIR}"

echo "==> Publishing PocketMC for linux-x64..."
dotnet publish PocketMC.App/PocketMC.App.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -o "${ROOT_DIR}/publish/linux-x64"

VERSION=$(grep '^version:' "${ROOT_DIR}/pocketmc.yml" | cut -d':' -f2 | xargs)
VERSION="${VERSION:-1.0.0.0}"

APPDIR="${ROOT_DIR}/PocketMC.AppDir"
rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin"
mkdir -p "${APPDIR}/usr/share/icons/hicolor/256x256/apps"
mkdir -p "${APPDIR}/usr/share/metainfo"

echo "==> Assembling AppDir (version ${VERSION})..."
cp -r "${ROOT_DIR}/publish/linux-x64/"* "${APPDIR}/usr/bin/"
cp "${ROOT_DIR}/PocketMC.App/Assets/icon.png" "${APPDIR}/pocketmc.png"
cp "${ROOT_DIR}/PocketMC.App/Assets/icon.png" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/pocketmc.png"

cat << EOF > "${APPDIR}/pocketmc.desktop"
[Desktop Entry]
Name=PocketMC
Comment=Local-first Minecraft server manager
Exec=PocketMC.App
Icon=pocketmc
Terminal=false
Type=Application
Categories=Game;Utility;
X-AppImage-Version=${VERSION}
X-AppImage-UpdateInformation=gh-releases-zsync|PocketMC|pocket-mc-linux-mac|latest|PocketMC-*-x86_64.AppImage.zsync
EOF

cat << EOF > "${APPDIR}/usr/share/metainfo/pocketmc.appdata.xml"
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>pocketmc.desktop</id>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>MIT</project_license>
  <name>PocketMC</name>
  <summary>Local-first Minecraft server manager</summary>
  <description>
    <p>Create, run, update, monitor, back up, and share Minecraft Java, Bedrock, and PocketMine servers from one native desktop app.</p>
  </description>
  <launchable type="desktop-id">pocketmc.desktop</launchable>
  <releases>
    <release version="${VERSION}" date="$(date +%Y-%m-%d)"/>
  </releases>
</component>
EOF

cat << 'EOF' > "${APPDIR}/AppRun"
#!/bin/sh
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/bin:${LD_LIBRARY_PATH:-}"
exec "${HERE}/usr/bin/PocketMC.App" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

echo "==> Fetching appimagetool..."
APPIMAGETOOL="${ROOT_DIR}/tools/appimagetool-x86_64.AppImage"
mkdir -p "${ROOT_DIR}/tools"

if [ ! -f "${APPIMAGETOOL}" ]; then
    curl -sSfL -o "${APPIMAGETOOL}" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x "${APPIMAGETOOL}"
fi

echo "==> Packaging AppImage..."
export ARCH=x86_64
"${APPIMAGETOOL}" \
    --appimage-extract-and-run \
    -u "gh-releases-zsync|PocketMC|pocket-mc-linux-mac|latest|PocketMC-*-x86_64.AppImage.zsync" \
    "${APPDIR}" \
    "${ROOT_DIR}/PocketMC-x86_64.AppImage"

echo "==> AppImage created successfully at ${ROOT_DIR}/PocketMC-x86_64.AppImage"
