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
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BOLD}${BLUE}===========================================${NC}"
echo -e "${BOLD}${BLUE}       PocketMC Universal Installer        ${NC}"
echo -e "${BOLD}${BLUE}===========================================${NC}"

# Handle uninstall command
if [ "$1" = "uninstall" ] || [ "$1" = "--uninstall" ]; then
  echo -e "${BOLD}Uninstalling PocketMC...${NC}"
  if [ "$OS" = "Linux" ]; then
    rm -rf "/opt/pocketmc" "$HOME/.local/share/pocketmc"
    rm -f "/usr/local/bin/pocketmc" "$HOME/.local/bin/pocketmc"
    rm -f "/usr/share/applications/pocketmc.desktop" "$HOME/.local/share/applications/pocketmc.desktop"
    rm -f "/usr/share/pixmaps/pocketmc.png" "$HOME/.local/share/icons/pocketmc.png"
    echo -e "${GREEN}[OK] PocketMC successfully uninstalled from Linux.${NC}"
  elif [ "$OS" = "Darwin" ]; then
    rm -rf "/Applications/PocketMC.app" "$HOME/Applications/PocketMC.app"
    rm -f "/usr/local/bin/pocketmc" "$HOME/.local/bin/pocketmc"
    echo -e "${GREEN}[OK] PocketMC successfully uninstalled from macOS.${NC}"
  fi
  exit 0
fi

# Robust downloader function supporting curl & wget with retries and timeout
fetch_file() {
  local url="$1"
  local output="$2"
  local max_retries=3
  local count=0
  local success=false

  while [ $count -lt $max_retries ]; do
    if command -v curl >/dev/null 2>&1; then
      if curl -L --progress-bar --connect-timeout 15 "$url" -o "$output"; then
        success=true
        break
      fi
    elif command -v wget >/dev/null 2>&1; then
      if wget -q --show-progress --timeout=15 --tries=1 "$url" -O "$output"; then
        success=true
        break
      fi
    else
      echo -e "${RED}[ERROR] Neither 'curl' nor 'wget' is available.${NC}"
      return 1
    fi

    count=$((count + 1))
    echo -e "${YELLOW}[WARNING] Download stalled or interrupted. Retrying ($count/$max_retries)...${NC}"
    sleep 2
  done

  if [ "$success" = false ]; then
    return 1
  fi
}

echo -e "==> Detecting system architecture..."

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

echo -e "[OK] Operating System: ${BOLD}$OS${NC} | Architecture: ${BOLD}$ARCH${NC} ($TARGET)"

# Ensure extraction tool is available
if [ "$EXT" = "tar.gz" ]; then
  command -v tar >/dev/null 2>&1 || { echo -e "${RED}[ERROR] 'tar' utility is required but not installed.${NC}"; exit 1; }
elif [ "$EXT" = "zip" ]; then
  command -v unzip >/dev/null 2>&1 || { echo -e "${RED}[ERROR] 'unzip' utility is required but not installed.${NC}"; exit 1; }
fi

echo -e "==> Resolving latest release version..."
TMP_VER_FILE=$(mktemp /tmp/pocketmc-ver.XXXXXX)
if fetch_file "https://api.github.com/repos/$REPO/releases/latest" "$TMP_VER_FILE" >/dev/null 2>&1; then
  LATEST_TAG=$(grep '"tag_name":' "$TMP_VER_FILE" | sed -E 's/.*"([^"]+)".*/\1/' || true)
fi
rm -f "$TMP_VER_FILE"

if [ -z "$LATEST_TAG" ]; then
  echo -e "${BLUE}[INFO] Could not query GitHub release metadata API. Using default version tag.${NC}"
  LATEST_TAG="v1.0.0"
fi

ASSET_NAME="PocketMC-${TARGET}.${EXT}"
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST_TAG/$ASSET_NAME"

echo -e "==> Fetching ${BOLD}$ASSET_NAME${NC} ($LATEST_TAG)..."
TMP_DIR=$(mktemp -d /tmp/pocketmc-install.XXXXXX)
trap 'rm -rf "$TMP_DIR"' EXIT

if ! fetch_file "$DOWNLOAD_URL" "$TMP_DIR/$ASSET_NAME"; then
  echo -e "${RED}[ERROR] Failed to download release asset from: $DOWNLOAD_URL${NC}"
  exit 1
fi

if [ "$OS" = "Linux" ]; then
  # Determine installation scope (Root / System-wide vs User-level)
  if [ "$EUID" -eq 0 ]; then
    echo -e "${BLUE}[INFO] Running with root privileges. Installing system-wide to /opt/pocketmc...${NC}"
    INSTALL_DIR="/opt/pocketmc"
    BIN_DIR="/usr/local/bin"
    DESKTOP_DIR="/usr/share/applications"
    ICON_DIR="/usr/share/pixmaps"
  else
    echo -e "${BLUE}[INFO] Running as standard user. Installing to $HOME/.local...${NC}"
    INSTALL_DIR="$HOME/.local/share/pocketmc"
    BIN_DIR="$HOME/.local/bin"
    DESKTOP_DIR="$HOME/.local/share/applications"
    ICON_DIR="$HOME/.local/share/icons"
  fi

  mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR" "$ICON_DIR"

  echo -e "==> Unpacking release archive to ${BOLD}$INSTALL_DIR${NC}..."
  rm -rf "$INSTALL_DIR"/*
  tar -xzf "$TMP_DIR/$ASSET_NAME" -C "$INSTALL_DIR"

  # Ensure binary permissions & symlink
  if [ -f "$INSTALL_DIR/PocketMC.App" ]; then
    chmod +x "$INSTALL_DIR/PocketMC.App"
    ln -sf "$INSTALL_DIR/PocketMC.App" "$BIN_DIR/pocketmc"
  fi

  # Copy / Download icon for launcher
  RAW_ICON_URL="https://raw.githubusercontent.com/$REPO/main/docs/assets/branding/logo.png"
  if [ -f "$INSTALL_DIR/docs/assets/branding/logo.png" ]; then
    cp "$INSTALL_DIR/docs/assets/branding/logo.png" "$ICON_DIR/pocketmc.png"
  else
    fetch_file "$RAW_ICON_URL" "$ICON_DIR/pocketmc.png" >/dev/null 2>&1 || true
  fi

  # Register Linux Desktop Launcher (.desktop)
  cat <<EOF > "$DESKTOP_DIR/pocketmc.desktop"
[Desktop Entry]
Name=PocketMC
Comment=Local-first Minecraft Server Manager
Exec=$BIN_DIR/pocketmc
Icon=$ICON_DIR/pocketmc.png
Terminal=false
Type=Application
Categories=Game;Utility;Network;
EOF

  chmod 644 "$DESKTOP_DIR/pocketmc.desktop"

  # Dynamic Firewall Configuration (When running as root)
  if [ "$EUID" -eq 0 ]; then
    echo -e "==> Checking Linux firewall configuration..."
    if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "active"; then
      echo -e "  -> UFW active. Allowing Minecraft Java (25565/tcp), Bedrock (19132/udp), Dashboard (8080/tcp)..."
      ufw allow 25565/tcp comment 'PocketMC Java Server' >/dev/null 2>&1 || true
      ufw allow 19132/udp comment 'PocketMC Bedrock Server' >/dev/null 2>&1 || true
      ufw allow 8080/tcp comment 'PocketMC Dashboard' >/dev/null 2>&1 || true
    elif command -v firewall-cmd >/dev/null 2>&1 && firewall-cmd --state >/dev/null 2>&1; then
      echo -e "  -> Firewalld active. Allowing Minecraft ports..."
      firewall-cmd --permanent --add-port=25565/tcp >/dev/null 2>&1 || true
      firewall-cmd --permanent --add-port=19132/udp >/dev/null 2>&1 || true
      firewall-cmd --permanent --add-port=8080/tcp >/dev/null 2>&1 || true
      firewall-cmd --reload >/dev/null 2>&1 || true
    fi
  fi

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC successfully installed on Linux.${NC}"
  echo -e "  - Binary Location: ${BOLD}$BIN_DIR/pocketmc${NC}"
  echo -e "  - Application Launcher: Registered (${BOLD}pocketmc.desktop${NC})"

  if [ "$EUID" -ne 0 ] && [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    echo -e ""
    echo -e "${BLUE}[NOTE] Ensure $BIN_DIR is present in your PATH. Add this to your shell profile (~/.bashrc or ~/.zshrc):${NC}"
    echo -e "  ${BOLD}export PATH=\"\$HOME/.local/bin:\$PATH\"${NC}"
  fi

elif [ "$OS" = "Darwin" ]; then
  if [ "$EUID" -eq 0 ] || [ -w "/Applications" ]; then
    TARGET_APP_DIR="/Applications"
  else
    TARGET_APP_DIR="$HOME/Applications"
    mkdir -p "$TARGET_APP_DIR"
  fi

  echo -e "==> Extracting PocketMC.app to ${BOLD}$TARGET_APP_DIR${NC}..."
  unzip -q -o "$TMP_DIR/$ASSET_NAME" -d "$TARGET_APP_DIR"

  # Strip macOS Gatekeeper quarantine attribute
  if command -v xattr >/dev/null 2>&1; then
    xattr -dr com.apple.quarantine "$TARGET_APP_DIR/PocketMC.app" 2>/dev/null || true
  fi

  # Symlink executable to user PATH
  if [ "$EUID" -eq 0 ]; then
    BIN_DIR="/usr/local/bin"
  else
    BIN_DIR="$HOME/.local/bin"
    mkdir -p "$BIN_DIR"
  fi

  if [ -f "$TARGET_APP_DIR/PocketMC.app/Contents/MacOS/PocketMC.App" ]; then
    ln -sf "$TARGET_APP_DIR/PocketMC.app/Contents/MacOS/PocketMC.App" "$BIN_DIR/pocketmc" 2>/dev/null || true
  fi

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC successfully installed on macOS.${NC}"
  echo -e "  - Application Bundle: ${BOLD}$TARGET_APP_DIR/PocketMC.app${NC}"
  echo -e "  - Launch directly via Spotlight or Launchpad."
fi
