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

RAW_VERSION=$(grep '^version:' "${ROOT_DIR}/pocketmc.yml" | cut -d':' -f2 | xargs)
VERSION=$(echo "${RAW_VERSION}" | sed 's/^v//')
VERSION="${VERSION:-1.0.0.0}"

if [ ! -d "${ROOT_DIR}/publish/linux-x64" ] || [ ! -f "${ROOT_DIR}/publish/linux-x64/PocketMC.App" ]; then
    echo "==> Publishing PocketMC standalone binary..."
    dotnet publish PocketMC.App/PocketMC.App.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true \
        -p:Version=${VERSION} \
        -o "${ROOT_DIR}/publish/linux-x64"
fi

echo "==> Ensuring Flathub remote and runtime dependencies..."
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo || true

echo "==> Building Flatpak bundle for io.github.pocketmc.app..."
BUILD_DIR="${ROOT_DIR}/_flatpak_build"
REPO_DIR="${ROOT_DIR}/_flatpak_repo"

rm -rf "${BUILD_DIR}" "${REPO_DIR}"

flatpak-builder --user --install-deps-from=flathub --force-clean --repo="${REPO_DIR}" "${BUILD_DIR}" flatpak/io.github.pocketmc.app.yml
flatpak build-bundle "${REPO_DIR}" "${ROOT_DIR}/PocketMC-x86_64.flatpak" io.github.pocketmc.app

echo "==> Flatpak bundle created successfully at ${ROOT_DIR}/PocketMC-x86_64.flatpak"
