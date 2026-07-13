# Requirements: PocketMC

**Defined:** 2026-07-13
**Core Value:** Provide a reliable, native cross-platform manager for Minecraft Java, Bedrock Dedicated Server, and PocketMine servers with unified process monitoring and lifecycle management on Linux and macOS.

## v1 Requirements

### Core Hosting & Platform (HOST)

- [ ] **HOST-01**: Application initializes using `Microsoft.Extensions.Hosting` to manage dependency injection and service lifecycles.
- [ ] **HOST-02**: Settings are stored in system-specific configuration directories (`~/.config/PocketMC/` on Linux and `~/Library/Application Support/PocketMC/` on macOS).
- [ ] **HOST-03**: Secure settings are stored using native platform APIs (`libsecret` on Linux, `Keychain` on macOS) with an AES-encrypted fallback for environments lacking native stores.
- [ ] **HOST-04**: Storage layout initializes directory structures for `Instances/`, `Backups/`, `Downloads/`, `Cache/`, and `Logs/` relative to the configuration root path.

### Instance Lifecycle Management (INST)

- [x] **INST-01**: User can create, delete, rename, clone, and import/export server instances as standard ZIP files.
- [x] **INST-02**: Supported engine types include Vanilla Java, Paper, Fabric, Forge, NeoForge, PocketMine-MP, and Bedrock Dedicated Server (BDS) Linux.
- [x] **INST-03**: Process runner monitors server lifecycle state machine: `Stopped`, `Starting`, `Running`, `Stopping`, `Crashed`, `Restarting`.
- [x] **INST-04**: Process runner implements process tree tracking and child process cleanup using standard POSIX signals (`SIGTERM`, `SIGKILL` timeout) without Windows dependencies.

### Runtime Provisioning (PROV)

- [ ] **PROV-01**: Java provisioning engine downloads and verifies Temurin OpenJDK runtimes (Java 8, 11, 17, 21, 25) using their official APIs.
- [ ] **PROV-02**: PHP provisioning engine downloads and extracts PocketMine binaries natively on Linux and macOS with automatic executable permissions configuration.
- [ ] **PROV-03**: Runtime paths and versions are verified before server launch (e.g., executing `java -version` and `php -v`).

### Server Dashboard & Console (DASH / CONS)

- [ ] **DASH-01**: Dashboard UI displays server cards with live status indicators and real-time CPU, RAM, and online player counts.
- [x] **CONS-01**: Terminal UI renders server console output with ANSI color support, log filtering, copy utilities, and logs saved to files.
- [x] **CONS-02**: Console detects server crashes and highlights them with a dedicated crash warning banner and troubleshooting tips.

### AI Intelligence (AI)

- [ ] **AI-01**: AI assistant integrates with OpenAI, Gemini, Claude, and Ollama providers to analyze logs and recommend fixes.
- [ ] **AI-02**: AI pipeline chunk-summarizes server logs to produce markdown troubleshooting reports while automatically redacting IP addresses.

### Marketplace Integration (MKT)

- [ ] **MKT-01**: User can search and install mods/plugins/addons from Modrinth and CurseForge.
- [ ] **MKT-02**: Marketplace engine handles mod dependencies and manages activation toggles in a JSON-backed manifest file.

### Tunnel Networking & Backups (NET / BCKP)

- [ ] **NET-01**: Network service configures and controls Playit, Cloudflare Tunnel, and Tailscale background processes.
- [ ] **NET-02**: Network dashboard monitors tunnel status and displays the public hostname/IP for players.
- [ ] **BCKP-01**: Backup engine creates standard ZIP archives of server files with SHA256 checksum verification.
- [ ] **BCKP-02**: Backup manager connects to Google Drive, Dropbox, and OneDrive for remote backup uploads.

### Player Management & Monitoring (PLYR / MON)

- [x] **PLYR-01**: Management UI permits modifying ops, whitelists, and banlists by watching and updating server JSON/TXT configurations.
- [x] **PLYR-02**: Online player list dynamically resolves player UUIDs and offers quick kick/ban actions.
- [ ] **MON-01**: Monitoring dashboard graphs historical resource metrics (CPU, RAM, Uptime) and tracks open network ports.

### Remote Control & Updates (RMT / UPDT)

- [ ] **RMT-01**: ASP.NET Core Minimal API starts a local web dashboard to view console output and execute commands.
- [ ] **RMT-02**: Remote dashboard uses secure sessions and supports pairing via a generated scan-to-pair QR code.
- [ ] **UPDT-01**: App checks for updates and installs them via Sparkle (macOS), AppImageUpdate (Linux), or direct GitHub Release downloads.

### Platform Visuals & Security (UI / SEC)

- [ ] **SEC-01**: Safe zip extraction guards against Zip Slip attacks using path validation.
- [ ] **UI-01**: UI matches system theme (light/dark) and features accent colors, rounded corners, and native title bars.

## v2 Requirements

### Advanced Monitoring & Multi-Server (MON2 / CLOUD)

- **MON2-01**: Live TPS (Ticks Per Second) graph via RCON or log regex scraping.
- **CLOUD-01**: Remote cluster management (syncing instances across multiple physical machines).
- **BCKP-03**: Incremental filesystem backups using rsync-like chunk transfers.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Windows support | Excluded to focus on Linux/macOS native abstractions (AppImage, Sparkle, POSIX process trees). |
| Direct Docker integration | Using native processes for simplified cross-platform deployment and debugging. |
| In-app server editing of binary files | Modifying server configurations only; binary jars/exes are treated as read-only assets. |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| HOST-01 | Phase 1 | Pending |
| HOST-02 | Phase 1 | Pending |
| HOST-03 | Phase 1 | Pending |
| HOST-04 | Phase 1 | Pending |
| PROV-01 | Phase 1 | Pending |
| PROV-02 | Phase 1 | Pending |
| PROV-03 | Phase 1 | Pending |
| SEC-01  | Phase 1 | Pending |
| INST-01 | Phase 2 | Complete |
| INST-02 | Phase 2 | Complete |
| INST-03 | Phase 2 | Complete |
| INST-04 | Phase 2 | Complete |
| CONS-01 | Phase 2 | Complete |
| CONS-02 | Phase 2 | Complete |
| PLYR-01 | Phase 2 | Complete |
| PLYR-02 | Phase 2 | Complete |
| DASH-01 | Phase 3 | Pending |
| MON-01  | Phase 3 | Pending |
| UI-01   | Phase 3 | Pending |
| MKT-01  | Phase 4 | Pending |
| MKT-02  | Phase 4 | Pending |
| BCKP-01 | Phase 4 | Pending |
| BCKP-02 | Phase 4 | Pending |
| AI-01   | Phase 4 | Pending |
| AI-02   | Phase 4 | Pending |
| NET-01  | Phase 5 | Pending |
| NET-02  | Phase 5 | Pending |
| RMT-01  | Phase 5 | Pending |
| RMT-02  | Phase 5 | Pending |
| UPDT-01 | Phase 5 | Pending |

**Coverage:**

- v1 requirements: 30 total
- Mapped to phases: 30
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-13*
*Last updated: 2026-07-13 after initial definition*
