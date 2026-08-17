#!/usr/bin/env bash
# PocketMC Universal Installer for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/PocketMC/pocket-mc-linux-mac/main/install.sh | bash

set -e

REPO="PocketMC/pocket-mc-linux-mac"
OS="$(uname -s)"
ARCH="$(uname -m)"

# Color formatting
BOLD='\033[1m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BOLD}${BLUE}===========================================${NC}"
echo -e "${BOLD}${BLUE}       PocketMC Universal Installer        ${NC}"
echo -e "${BOLD}${BLUE}===========================================${NC}"

# Handle uninstall command
if [ "$1" = "uninstall" ] || [ "$1" = "--uninstall" ]; then
  echo -e "${BOLD}Removing PocketMC...${NC}"
  if [ "$OS" = "Linux" ]; then
    rm -rf "$HOME/.local/share/pocketmc"
    rm -f "$HOME/.local/bin/pocketmc"
    rm -f "$HOME/.local/share/applications/pocketmc.desktop"
    echo -e "${GREEN}[OK] PocketMC uninstalled from Linux.${NC}"
  elif [ "$OS" = "Darwin" ]; then
    rm -rf "/Applications/PocketMC.app" "$HOME/Applications/PocketMC.app"
    rm -f "$HOME/.local/bin/pocketmc"
    echo -e "${GREEN}[OK] PocketMC uninstalled from macOS.${NC}"
  fi
  exit 0
fi

echo -e "==> Detecting system profile..."

case "$OS" in
  Linux)
    case "$ARCH" in
      x86_64|amd64) TARGET="linux-x64"; EXT="tar.gz" ;;
      aarch64|arm64) TARGET="linux-arm64"; EXT="tar.gz" ;;
      *) echo -e "${RED}[ERROR] Unsupported Linux architecture: $ARCH${NC}"; exit 1 ;;
    esac
    ;;
  Darwin)
    case "$ARCH" in
      x86_64) TARGET="osx-x64"; EXT="zip" ;;
      arm64)  TARGET="osx-arm64"; EXT="zip" ;;
      *) echo -e "${RED}[ERROR] Unsupported macOS architecture: $ARCH${NC}"; exit 1 ;;
    esac
    ;;
  *)
    echo -e "${RED}[ERROR] Unsupported operating system: $OS${NC}"
    exit 1
    ;;
esac

echo -e "[OK] OS: ${BOLD}$OS${NC} | Architecture: ${BOLD}$ARCH${NC} ($TARGET)"

# Dependency check
command -v curl >/dev/null 2>&1 || { echo -e "${RED}[ERROR] 'curl' is required but not installed.${NC}"; exit 1; }

if [ "$EXT" = "tar.gz" ]; then
  command -v tar >/dev/null 2>&1 || { echo -e "${RED}[ERROR] 'tar' is required but not installed.${NC}"; exit 1; }
elif [ "$EXT" = "zip" ]; then
  command -v unzip >/dev/null 2>&1 || { echo -e "${RED}[ERROR] 'unzip' is required but not installed.${NC}"; exit 1; }
fi

echo -e "==> Querying GitHub release metadata..."
LATEST_TAG=$(curl -s "https://api.github.com/repos/$REPO/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/' || true)

if [ -z "$LATEST_TAG" ]; then
  echo -e "${BLUE}[INFO] Could not query GitHub release API. Falling back to default release version tag.${NC}"
  LATEST_TAG="v1.0.0"
fi

ASSET_NAME="PocketMC-${TARGET}.${EXT}"
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST_TAG/$ASSET_NAME"

echo -e "==> Downloading ${BOLD}$ASSET_NAME${NC} ($LATEST_TAG)..."
TMP_DIR=$(mktemp -d /tmp/pocketmc-install.XXXXXX)
trap 'rm -rf "$TMP_DIR"' EXIT

if ! curl -fsSL "$DOWNLOAD_URL" -o "$TMP_DIR/$ASSET_NAME"; then
  echo -e "${RED}[ERROR] Failed to download release asset from: $DOWNLOAD_URL${NC}"
  exit 1
fi

if [ "$OS" = "Linux" ]; then
  INSTALL_DIR="$HOME/.local/share/pocketmc"
  BIN_DIR="$HOME/.local/bin"
  DESKTOP_DIR="$HOME/.local/share/applications"

  mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR"

  echo -e "==> Extracting files to ${BOLD}$INSTALL_DIR${NC}..."
  rm -rf "$INSTALL_DIR"/*
  tar -xzf "$TMP_DIR/$ASSET_NAME" -C "$INSTALL_DIR"

  # Ensure binary is executable
  if [ -f "$INSTALL_DIR/PocketMC.App" ]; then
    chmod +x "$INSTALL_DIR/PocketMC.App"
    ln -sf "$INSTALL_DIR/PocketMC.App" "$BIN_DIR/pocketmc"
  fi

  # Write Desktop Launcher
  cat <<EOF > "$DESKTOP_DIR/pocketmc.desktop"
[Desktop Entry]
Name=PocketMC
Comment=Local-first Minecraft Server Manager
Exec=$BIN_DIR/pocketmc
Icon=$INSTALL_DIR/docs/assets/branding/logo.png
Terminal=false
Type=Application
Categories=Game;Utility;Network;
EOF

  chmod +x "$DESKTOP_DIR/pocketmc.desktop"

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC installed on Linux.${NC}"
  echo -e "  - Binary: ${BOLD}$BIN_DIR/pocketmc${NC}"
  echo -e "  - Desktop Shortcut: Registered (${BOLD}pocketmc.desktop${NC})"
  
  if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    echo -e ""
    echo -e "${BLUE}[NOTE] Ensure $BIN_DIR is in your PATH. Add this line to your shell profile (~/.bashrc or ~/.zshrc):${NC}"
    echo -e "  ${BOLD}export PATH=\"\$HOME/.local/bin:\$PATH\"${NC}"
  fi

elif [ "$OS" = "Darwin" ]; then
  TARGET_APP_DIR="/Applications"
  if [ ! -w "$TARGET_APP_DIR" ]; then
    TARGET_APP_DIR="$HOME/Applications"
    mkdir -p "$TARGET_APP_DIR"
  fi

  echo -e "==> Extracting PocketMC.app to ${BOLD}$TARGET_APP_DIR${NC}..."
  unzip -q -o "$TMP_DIR/$ASSET_NAME" -d "$TARGET_APP_DIR"

  # Clear macOS Gatekeeper quarantine attribute
  if command -v xattr >/dev/null 2>&1; then
    xattr -dr com.apple.quarantine "$TARGET_APP_DIR/PocketMC.app" 2>/dev/null || true
  fi

  # Symlink to PATH for CLI usage
  BIN_DIR="$HOME/.local/bin"
  mkdir -p "$BIN_DIR"
  if [ -f "$TARGET_APP_DIR/PocketMC.app/Contents/MacOS/PocketMC.App" ]; then
    ln -sf "$TARGET_APP_DIR/PocketMC.app/Contents/MacOS/PocketMC.App" "$BIN_DIR/pocketmc"
  fi

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC installed on macOS.${NC}"
  echo -e "  - App Bundle: ${BOLD}$TARGET_APP_DIR/PocketMC.app${NC}"
  echo -e "  - Launch directly via Spotlight or Launchpad."
fi
