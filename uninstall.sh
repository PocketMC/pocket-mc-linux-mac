#!/usr/bin/env bash
# PocketMC Universal Uninstaller for Linux and macOS
# Usage: ./uninstall.sh [--yes]

set -e

OS="$(uname -s)"

# Color formatting
BOLD='\033[1m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BOLD}${BLUE}===========================================${NC}"
echo -e "${BOLD}${BLUE}      PocketMC Universal Uninstaller       ${NC}"
echo -e "${BOLD}${BLUE}===========================================${NC}"

# Confirmation prompt unless --yes or -y flag is passed
AUTO_CONFIRM=false
if [ "$1" = "--yes" ] || [ "$1" = "-y" ]; then
  AUTO_CONFIRM=true
fi

if [ "$AUTO_CONFIRM" = false ]; then
  echo -e "${YELLOW}This action will remove the PocketMC application, binary symlinks, desktop shortcuts, and icons.${NC}"
  if [ -c /dev/tty ]; then
    read -p "Are you sure you want to proceed with uninstallation? (y/N): " CONFIRM </dev/tty
  else
    read -p "Are you sure you want to proceed with uninstallation? (y/N): " CONFIRM
  fi
  case "$CONFIRM" in
    [yY][eE][sS]|[yY]) ;;
    *) echo -e "${BLUE}[INFO] Uninstallation cancelled.${NC}"; exit 0 ;;
  esac
fi

echo -e "==> Removing PocketMC installation files..."

if [ "$OS" = "Linux" ]; then
  # Remove system-wide root installations if run as root or if files exist
  if [ "$EUID" -eq 0 ] || [ -d "/opt/pocketmc" ]; then
    echo -e "  - Removing system-wide files (/opt/pocketmc, /usr/local/bin/pocketmc)..."
    rm -rf "/opt/pocketmc"
    rm -f "/usr/local/bin/pocketmc"
    rm -f "/usr/share/applications/pocketmc.desktop"
    rm -f "/usr/share/pixmaps/pocketmc.png"
  fi

  # Remove user-level installations
  echo -e "  - Removing user-level files (~/.local/share/pocketmc, ~/.local/bin/pocketmc)..."
  rm -rf "$HOME/.local/share/pocketmc"
  rm -f "$HOME/.local/bin/pocketmc"
  rm -f "$HOME/.local/share/applications/pocketmc.desktop"
  rm -f "$HOME/.local/share/icons/pocketmc.png"

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC successfully uninstalled from Linux.${NC}"

elif [ "$OS" = "Darwin" ]; then
  echo -e "  - Removing Application bundle (/Applications/PocketMC.app)..."
  rm -rf "/Applications/PocketMC.app" "$HOME/Applications/PocketMC.app"
  rm -f "/usr/local/bin/pocketmc" "$HOME/.local/bin/pocketmc"

  echo -e ""
  echo -e "${GREEN}${BOLD}[SUCCESS] PocketMC successfully uninstalled from macOS.${NC}"
else
  echo -e "${RED}[ERROR] Unsupported operating system: $OS${NC}"
  exit 1
fi
