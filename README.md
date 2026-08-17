<div align="center">

<table border="0" cellpadding="16">
  <tr>
    <td align="center" width="200">
      <img src="docs/assets/branding/logo.png" alt="PocketMC" width="180" />
    </td>
    <td align="center">
      <h1 style="border: none; margin-bottom: 10px;">PocketMC Linux & macOS</h1>
      <p><b>Local-first Minecraft server management, without the terminal mess.</b></p>
      <p>Create, run, update, monitor, back up, and share Minecraft Java, Bedrock, and PocketMine servers from one native desktop app.</p>
      <a href="https://github.com/PocketMC/pocket-mc-linux-mac/actions"><img src="https://img.shields.io/github/actions/workflow/status/PocketMC/pocket-mc-linux-mac/ci.yml?branch=main&style=flat-square&logo=github" alt="Build" /></a>
      <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8" /></a>
      <a href="https://github.com/PocketMC/pocket-mc-linux-mac/releases"><img src="https://img.shields.io/github/v/release/PocketMC/pocket-mc-linux-mac?style=flat-square" alt="Release" /></a>
      <a href="https://discord.gg/mWdMr8Mc2m"><img src="https://img.shields.io/badge/Discord-Join-%235865F2?style=flat-square&logo=discord" alt="Discord" /></a>
      <br><br>
    </td>
  </tr>
</table>

</div>

---

PocketMC is a native, cross-platform desktop application built using Avalonia UI and .NET 8 for Linux and macOS. It handles software downloads, isolated instances, managed Java and PHP runtimes, startup and shutdown lifecycle monitoring, real-time performance graphs, player lists, automated backups, external cloud replication (Dropbox, Google Drive, OneDrive), CurseForge/Modrinth addon integration, Playit.gg/Cloudflared tunnel provisioning, and a paired remote web dashboard.

Your servers run locally on your hardware. PocketMC is not a cloud hosting service, not a Minecraft launcher, and does not require Docker or system-level virtualization.

---

## Comparative Workflow

| Before PocketMC | With PocketMC |
| :--- | :--- |
| Manually find, download, and configure server executables and jars. | Server binaries and matching runtime components are fully managed. |
| Fragmented terminal processes and config files scattered across disks. | Isolated instances maintained under a single structured root. |
| Complex configuration of local reverse proxies and port-forwarding. | Built-in provisioning of Playit.gg and Cloudflared tunnels. |
| Manual, irregular back-up scripts subject to failure. | Scheduled backups with zip extraction and cloud provider synchronization. |
| Vulnerability to orphaned processes and terminal lockups on close. | Automated background process tree cleanup on application exit. |
| Accessing server controls is restricted to the host machine. | Secure web dashboard pairing via QR code or clipboard URL. |

---

## Supported Server Engines

<table border="0" align="center" cellpadding="8">
  <tr align="center">
    <td><img src="docs/assets/icons/vanilla.png" alt="Vanilla Java" height="50" /></td>
    <td><img src="docs/assets/icons/papermc.png" alt="Paper" height="50" /></td>
    <td><img src="docs/assets/icons/fabric.png" alt="Fabric" height="50" /></td>
    <td><img src="docs/assets/icons/forge.png" alt="Forge" height="50" /></td>
    <td><img src="docs/assets/icons/neoforge.png" alt="NeoForge" height="50" /></td>
    <td><img src="docs/assets/icons/bds.png" alt="Bedrock Dedicated Server" height="50" /></td>
    <td><img src="docs/assets/icons/pocketmine-mp.png" alt="PocketMine-MP" height="50" /></td>
  </tr>
  <tr align="center" valign="top">
    <td><sub><b>Vanilla Java</b></sub></td>
    <td><sub><b>Paper</b></sub></td>
    <td><sub><b>Fabric</b></sub></td>
    <td><sub><b>Forge</b></sub></td>
    <td><sub><b>NeoForge</b></sub></td>
    <td><sub><b>Bedrock (BDS)</b></sub></td>
    <td><sub><b>PocketMine-MP</b></sub></td>
  </tr>
</table>

---

## Installation & Setup

### Universal Installer (Linux & macOS)

Install or update PocketMC on Linux or macOS using a single command:

```bash
curl -fsSL https://raw.githubusercontent.com/PocketMC/pocket-mc-linux-mac/main/install.sh | bash
```

### Uninstallation

Uninstall PocketMC anytime using the standalone uninstaller script:

```bash
curl -fsSL https://raw.githubusercontent.com/PocketMC/pocket-mc-linux-mac/main/uninstall.sh | bash
```

Or from a local clone:
```bash
./uninstall.sh
```

### Prebuilt Downloads & Manual Installation

Prebuilt installers and standalone archives are available on [GitHub Releases](https://github.com/PocketMC/pocket-mc-linux-mac/releases):

- **macOS Disk Image (`.dmg` Drag-and-Drop)**:
  - Apple Silicon (M1-M4): `PocketMC-osx-arm64.dmg`
  - Intel Macs: `PocketMC-osx-x64.dmg`
  Open the downloaded `.dmg` and drag `PocketMC.app` into your `Applications` folder.

- **Linux Archive (`.tar.gz`)**:
  ```bash
  tar -xzf PocketMC-linux-x64.tar.gz
  ./publish/PocketMC.App
  ```

- **macOS Archive (`.zip`)**:
  - Apple Silicon (M1-M4): `PocketMC-osx-arm64.zip`
  - Intel Macs: `PocketMC-osx-x64.zip`
  Extract the archive and move `PocketMC.app` to `/Applications`.

---

## Core Capabilities

### Instance Lifecycle Management
- Creation and deletion of isolated server instance directories.
- Graceful shutdown orchestration via RCON with stdin stream fallback.
- Pre-launch port conflict validation.
- Process supervision preventing orphaned background server processes on crash or close.
- Custom JVM arguments and startup options.

### Managed Runtimes
- Automatic detection and retrieval of matching Java runtimes based on server engine version.
- Standardized PHP execution bundles for PocketMine-MP instances.
- Zero manual environment variable or system PATH modifications.

### Integrated Proxy Tunnels
- Instant Playit.gg agent tunnel provisioning.
- Cloudflared tunnel creation with continuous stream consumption.
- System keychain storage (macOS Keychain / Linux Secret Service) with AES fallback.

### Backups & Synchronization
- Local zip-archived instance packaging with configurable retention limits.
- Automated backup scheduling engine.
- Direct synchronization with Dropbox, Google Drive, and OneDrive.

### Addons Marketplace
- Search and install mods, plugins, and resource packs from CurseForge and Modrinth APIs.
- Automatic file routing to target instance directory structures.

### Remote Control Web Dashboard
- Password-authenticated web companion panel.
- Live websocket console output streaming and remote execution controls.
- Update notifications powered by GitHub Releases API.

---

## System Architecture

PocketMC is structured using a service-oriented, layered architecture:

- **PocketMC.Core:** Domain models, service interfaces, update specifications.
- **PocketMC.Platform:** Native system integration, credential store wrappers.
- **PocketMC.Infrastructure:** Process supervisors, file services, RCON client, backup scheduler, cloud sync.
- **PocketMC.RemoteControl:** Embedded web server, websocket pipelines, tunnel executors.
- **PocketMC.App:** Desktop presentation layer built with Avalonia UI and MVVM.

---

## Development & Building

### Prerequisites
- .NET 8.0 SDK
- Nix development shell available via `nix-shell` (optional).

### Run from Source
```bash
git clone https://github.com/PocketMC/pocket-mc-linux-mac.git
cd pocket-mc-linux-mac
dotnet run --project PocketMC.App/PocketMC.App.csproj
```

### Local Test Execution
```bash
dotnet test
```

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for full details.
