# Roadmap: PocketMC

## Overview

This roadmap lays out the creation of PocketMC as a native, cross-platform Minecraft and PocketMine server manager for Linux and macOS. It follows a Horizontal Layers structure to establish a robust infrastructure core and runtime provisioner first, followed by the instance runner engine, UI dashboards, marketplace and backups extension system, and finally the remote dashboard and tunnel networking overlays.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

- [x] **Phase 1: Core Foundation & Provisioning** - Set up the DI host container, cross-platform secure settings storage, and automated Java/PHP runtime installers. (completed 2026-07-13)
- [x] **Phase 2: Instance Lifecycle & Console** - Implement instance management CRUD, POSIX process tree tracking, colored console logs, and player list configurations. (completed 2026-07-13)
- [ ] **Phase 3: Dashboard & Monitoring** - Build the server dashboard UI with live resource utilization graphs and native visual styling.
- [ ] **Phase 4: Marketplace, Backups & AI** - Add CurseForge/Modrinth downloads, scheduled cloud backups, and AI crash log diagnosis.
- [ ] **Phase 5: Remote Control, Networking & Updates** - Add the remote Minimal API dashboard with QR pairing, tunnel integrations (Playit/Cloudflare), and native auto-updaters.

## Phase Details

### Phase 1: Core Foundation & Provisioning

**Goal**: Build the core hosting environment, cross-platform secure storage, path safety, and Java/PHP runtime provisioning managers.
**Depends on**: Nothing (first phase)
**Requirements**: [HOST-01, HOST-02, HOST-03, HOST-04, PROV-01, PROV-02, PROV-03, SEC-01]
**Success Criteria** (what must be TRUE):

  1. Application initializes successfully using `Microsoft.Extensions.Hosting` to manage background services.
  2. Configuration directories are set up correctly on the host filesystem under standard OS configuration paths.
  3. Sensitive credentials are encrypted natively using libsecret (Linux) or Keychain (macOS) with AES fallback.
  4. Runtimes for Java (Temurin) and PHP (PocketMine) are automatically downloaded, verified via SHA256, and validated.

**Plans**: 2 plans

Plans:

- [x] 01-01: Implement hosting environment, DI containers, directory layout, and secure settings service.
- [x] 01-02: Implement Java/PHP runtime provisioners with hash checking and version command verifiers.

### Phase 2: Instance Lifecycle & Console

**Goal**: Implement the instance lifecycle service, process tree runner with POSIX signal cleanup, console output viewer, and basic player list configurations.
**Depends on**: Phase 1
**Requirements**: [INST-01, INST-02, INST-03, INST-04, CONS-01, CONS-02, PLYR-01, PLYR-02]
**Success Criteria** (what must be TRUE):

  1. Server instances can be created, deleted, renamed, cloned, and imported/exported securely as ZIP archives.
  2. Server processes are tracked via a state machine that handles graceful stops (SIGTERM) and forcefully kills (SIGKILL) child process trees.
  3. Real-time server log feed supports ANSI coloring, filtering, search, and session history saving.
  4. Whitelist, ops, and banlists can be managed and watched dynamically on disk.

**Plans**: 2 plans

Plans:

**Wave 1**

- [x] 02-01: Create instance CRUD service with Zip Slip safety, metadata manifests, and configuration file parsers.

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 02-02: Build process tree runner, POSIX signal sender, console terminal logging engine, and player list manager.

### Phase 3: Dashboard & Monitoring

**Goal**: Design the server management dashboard cards with live resource monitoring graphs and native platform UI styling.
**Depends on**: Phase 2
**Requirements**: [DASH-01, MON-01, UI-01]
**Success Criteria** (what must be TRUE):

  1. Main dashboard renders server cards showing live engine type, player count, and CPU/RAM status.
  2. Performance graph shows historical resource utilization (CPU, RAM, Uptime) and open server ports.
  3. UI supports dark/light themes natively with rounded corners and platform blur effects.

**Plans**: 1 plan

Plans:

- [ ] 03-01: Build Avalonia UI dashboard pages, metric polling service, and system theme bindings.

### Phase 4: Marketplace, Backups & AI

**Goal**: Integrate CurseForge/Modrinth marketplace, ZIP backup engine with cloud uploading, and AI log analysis pipelines.
**Depends on**: Phase 2
**Requirements**: [MKT-01, MKT-02, BCKP-01, BCKP-02, AI-01, AI-02]
**Success Criteria** (what must be TRUE):

  1. Add-ons from CurseForge and Modrinth can be searched and installed with dependency resolution.
  2. Server files are zipped and validated via SHA256 before uploading to Google Drive, Dropbox, or OneDrive.
  3. Log files are chunked and analyzed using OpenAI, Gemini, Claude, or Ollama, with IP addresses fully redacted.

**Plans**: 2 plans

Plans:

- [ ] 04-01: Add Marketplace download manager, zip backup scheduler, and cloud API upload client.
- [ ] 04-02: Add AI diagnostic log pipeline with chunking summarizers and privacy redaction rules.

### Phase 5: Remote Control, Networking & Updates

**Goal**: Configure Playit/Cloudflare tunnels, launch ASP.NET Core Minimal API for secure remote control with QR pairing, and set up native auto-updaters.
**Depends on**: Phase 3, Phase 4
**Requirements**: [NET-01, NET-02, RMT-01, RMT-02, UPDT-01]
**Success Criteria** (what must be TRUE):

  1. App starts a local Minimal API server that routes console streams and command execution to authenticated clients.
  2. QR code pairing establishes a secure authenticated session between the app and external devices.
  3. Tunnels (Playit, Cloudflare) execute as background processes and display public connection addresses.
  4. Native updaters check for and install updates via Sparkle (macOS) and AppImageUpdate (Linux).

**Plans**: 2 plans

Plans:

- [ ] 05-01: Build Minimal API remote panel with QR code pairing generator and console socket streams.
- [ ] 05-02: Add tunnel wrapper execution service and Sparkle/AppImage native updaters.

## Progress

Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Core Foundation & Provisioning | 2/2 | Complete   | 2026-07-13 |
| 2. Instance Lifecycle & Console | 2/2 | Complete    | 2026-07-13 |
| 3. Dashboard & Monitoring | 0/1 | Not started | - |
| 4. Marketplace, Backups & AI | 0/2 | Not started | - |
| 5. Remote Control, Networking & Updates | 0/2 | Not started | - |
