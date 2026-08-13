#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${ROOT_DIR}"

if ! command -v flatpak-builder &> /dev/null; then
    echo "ERROR: flatpak-builder is required to build Flatpak packages."
    echo "Install flatpak-builder using your package manager (e.g. sudo apt install flatpak-builder or sudo dnf install flatpak-builder)."
    exit 1
fi

echo "==> Building Flatpak bundle for io.github.pocketmc.app..."
BUILD_DIR="${ROOT_DIR}/_flatpak_build"
REPO_DIR="${ROOT_DIR}/_flatpak_repo"

rm -rf "${BUILD_DIR}" "${REPO_DIR}"

flatpak-builder --force-clean --repo="${REPO_DIR}" "${BUILD_DIR}" flatpak/io.github.pocketmc.app.yml
flatpak build-bundle "${REPO_DIR}" "${ROOT_DIR}/PocketMC-x86_64.flatpak" io.github.pocketmc.app

echo "==> Flatpak bundle created successfully at ${ROOT_DIR}/PocketMC-x86_64.flatpak"
