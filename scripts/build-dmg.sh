#!/usr/bin/env bash
# macOS DMG Packaging Script for PocketMC
# Usage: ./scripts/build-dmg.sh <rid> <version> [publish_dir]

set -e

RID="${1:-osx-arm64}"
VERSION="${2:-1.0.0}"
PUBLISH_DIR="${3:-publish}"
OUTPUT_DMG="PocketMC-${RID}.dmg"

echo "==> Packaging macOS DMG for ${RID} (v${VERSION})..."

if [ ! -d "${PUBLISH_DIR}" ]; then
  echo "Error: Publish directory '${PUBLISH_DIR}' does not exist."
  exit 1
fi

TMP_BUILD_DIR=$(mktemp -d /tmp/pocketmc-dmg.XXXXXX)
trap 'rm -rf "${TMP_BUILD_DIR}"' EXIT

APP_DIR="${TMP_BUILD_DIR}/PocketMC.app"
mkdir -p "${APP_DIR}/Contents/MacOS"
mkdir -p "${APP_DIR}/Contents/Resources"

# Copy published application files
cp -R "${PUBLISH_DIR}/"* "${APP_DIR}/Contents/MacOS/"
if [ -f "${APP_DIR}/Contents/MacOS/PocketMC.App" ]; then
  chmod +x "${APP_DIR}/Contents/MacOS/PocketMC.App"
fi

# Generate Info.plist
cat <<EOF > "${APP_DIR}/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>PocketMC.App</string>
    <key>CFBundleIconFile</key>
    <string>pocketmc.icns</string>
    <key>CFBundleIdentifier</key>
    <string>io.github.pocketmc.app</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>PocketMC</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

# Copy icon if present
if [ -f "docs/assets/branding/logo.png" ]; then
  cp "docs/assets/branding/logo.png" "${APP_DIR}/Contents/Resources/pocketmc.png"
fi

# Sign the application bundle with ad-hoc signature
if command -v codesign >/dev/null 2>&1; then
  echo "==> Applying ad-hoc codesign to PocketMC.app..."
  codesign --force --deep --sign - "${APP_DIR}"
fi

# Prepare DMG volume staging with drag-and-drop /Applications shortcut
DMG_STAGING="${TMP_BUILD_DIR}/dmg_staging"
mkdir -p "${DMG_STAGING}"
cp -R "${APP_DIR}" "${DMG_STAGING}/"
ln -sf /Applications "${DMG_STAGING}/Applications"

if command -v hdiutil >/dev/null 2>&1; then
  echo "==> Creating DMG disk image using hdiutil..."
  hdiutil create -volname "PocketMC" \
    -srcfolder "${DMG_STAGING}" \
    -ov -format UDZO \
    "${OUTPUT_DMG}"
  echo "[SUCCESS] Generated ${OUTPUT_DMG}"
else
  echo "Warning: 'hdiutil' is only available natively on macOS. Staging .app bundle created."
fi
