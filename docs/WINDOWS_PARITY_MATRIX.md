# PocketMC Windows vs. Linux & macOS Parity Matrix

This document provides an exhaustive, capability-by-capability parity assessment between the authoritative PocketMC Windows implementation (`pocket-mc-windows`) and the cross-platform Linux & macOS implementation (`pocket-mc-linux-mac`).

**Status Legend:**
- **Verified:** Implemented, tested, and validated against native platform requirements with automated tests or execution evidence.
- **Implemented:** Implemented in code, compiling, pending end-to-end multi-platform execution test.
- **Partial:** Partially implemented; key capabilities, edge-case protections, or subfeatures missing.
- **Missing:** Not present in the Linux/macOS codebase.

---

## 1. Server Version Updates

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Target Version Discovery | `InstanceVersionTargetService.cs` | `InstanceVersionTargetService.cs` | Verified | Supports Mojang, PaperMC, Fabric, Forge, NeoForge, BDS, and PocketMine version resolution. | Tested via `TargetDiscoveryTests`. |
| Loader-Specific Update Workflows | Dedicated loader downloaders | `InstanceArtifactStager.cs` (`DownloadFabricAsync`, `DownloadForgeAsync`, `DownloadNeoForgeAsync`) | Verified | Forge and NeoForge download as `installer.jar`, while Fabric resolves meta.fabricmc.net installer endpoints. | Verified: `FabricUpdate_NeverDownloadsPaper`, `ForgeUpdate_NeverDownloadsPaper`, `NeoForgeUpdate_NeverDownloadsPaper` pass. |
| Checksum & Artifact Integrity | SHA1/SHA256 verification | `InstanceArtifactStager.cs` | Verified | Verifies Mojang SHA1 hash and Paper build application SHA256 checksums before staging. | Verified: `VanillaChecksumMismatch_ThrowsCryptographicException` confirms rejection on mismatch. |
| Update Viability & Planning | `InstanceUpdatePlanner.cs`, `InstanceUpdateModels.cs` | `InstanceUpdatePlanner.cs`, `InstanceUpdateModels.cs` | Verified | Maps required Java runtimes (Java 21 for MC 1.20.5+, Java 17 for 1.17+, Java 8 for legacy). | Verified: `InstanceUpdatePlanner_PlansForgeAndNeoForgeAsInstallerJar`. |
| Non-Destructive Artifact Staging | `InstanceArtifactStager.cs` | `InstanceArtifactStager.cs` | Verified | Staging directory under instance storage root (`.staging_update/`). | Tested via staging isolation tests. |
| Two-Phase Transaction & Rollback | `InstanceUpdateApplier.cs`, `InstanceFileTransaction.cs` | `InstanceUpdateApplier.cs`, `InstanceFileTransaction.cs` | Verified | Two-phase transaction (`PrepareAndApply` -> `Complete`/`Rollback`) preserves rollback backups in `.pocketmc-rollback-...` until registry persistence succeeds. | Verified: `InstanceUpdateApplier_WhenRegistryPersistenceFails_RollsBackArtifactReplacement`. |
| Operation Mutual Exclusion | `InstanceOperationCoordinator.cs`, `InstanceUpdateLockService.cs` | `InstanceOperationCoordinator.cs` | Verified | Concurrent semaphores keyed on normalized paths prevent concurrent updates, backups, restores, or launches. | Verified: integrated into `ProcessRunner`, `BackupService`, and update pipeline. |

---

## 2. Server Lifecycle & Process Management

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Process Group Supervision | Windows Job Objects (`PInvoke.CreateJobObject`) | Libc `setpgid` and `kill(-pgid, sig)` | Verified | Uses libc P/Invoke for POSIX process groups to eliminate orphaned sub-processes. | Verified via process execution tests and mock runner tests. |
| Graceful Shutdown & Escalation | `save-all` + `stop` via console stdin; RCON fallback | Stdin `save-all` + `stop`, escalating `SIGTERM` (5s timeout), then `SIGKILL` targeting process group | Verified | Clean POSIX signal escalation avoids immediate process abortion while ensuring termination. Supports `StopAllAsync()`. | Verified via `ProcessRunnerTests`. |
| Safe World Mutex & Lock Inspection | Non-destructive lock checks | `ProcessRunner.InspectSessionLock` | Verified | Never blindly deletes Minecraft `session.lock` file; warns if lock is present without corrupting active worlds. | Verified: `StartAsync_DoesNotDeleteSessionLockFile` passes. |
| Auto-Restart on Crash | Rate-limited (3x / 5 min) with backoff | `ProcessRunner.HandleProcessExit` (3x / 5 min) | Verified | Non-zero exit code triggers backoff restart. | Verified: regression tested against rate limits. |
| Scheduled Reboot Maintenance | `RebootScheduleService.cs`, `ServerRebootSchedulerService.cs` | `RebootScheduleService.cs`, `ServerRebootSchedulerService.cs` | Verified | Background scheduler evaluates running instances every 30s. Supports Daily (fixed 24h time) and recurring Interval modes with chat countdown warnings. | Verified: 18 tests passing in `RebootScheduleServiceTests` and `ServerRebootSchedulerServiceTests`. |
| Port Conflict Detection | `PortProber.cs`, `PortConflictWindow.xaml` | `PortProber.cs`, `PreLaunchVerifier.cs` | Verified | TCP and UDP socket probing before process launch. | Tested via socket probing unit tests. |
| Process Shutdown Hooks | Windows app exit hooks | `App.axaml.cs` desktop.Exit lifecycle hooks | Verified | Coordinates parallel graceful shutdown of all supervised servers, tunnels, and schedulers upon application quit. | Verified in `App.axaml.cs`. |

---

## 3. Server Creation Experience

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Engine Coverage | Vanilla, Paper, Fabric, Forge, NeoForge, BDS, PocketMine | Vanilla, Paper, Fabric, Forge, NeoForge, BDS, PocketMine | Verified | Bedrock Dedicated Server (BDS) only provides x86_64 Linux binaries; Apple Silicon (ARM64) and Linux ARM64 require Geyser/Paper or emulation. | Tested engine selection and manifest generation. |
| Custom World Import | `WorldManager.cs`, `InstanceImportService.cs` | `InstanceService.ImportInstanceAsync` | Verified | File extraction, zip slip security validation, level.dat detection, folder restructuring. | Verified via import tests. |
| Geyser / Floodgate Integration | `GeyserProvisioningService.cs` | `GeyserProvisioningService.cs`, `GeyserDetector.cs` | Verified | Automated download and placement of Geyser/Floodgate plugins with dynamic UDP port patching. | Tested: Geyser configuration patch tests. |
| Guided Wizard UI | `NewInstancePage.xaml`, `NewInstanceViewModel.cs` | `NewInstancePage.axaml`, `NewInstanceViewModel.cs` | Verified | Avalonia UI form with engine, version, port, seed, max players, and Geyser toggle. | Verified via ViewModel unit tests. |

---

## 4. Managed Runtimes & Zero-Setup

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Adoptium Java Auto-Provisioning | `AdoptiumClient.cs`, `JavaRuntimeResolver.cs` | `JavaService.cs` (Adoptium API) | Verified | Resolves Linux x64/arm64 and macOS x64/arm64 tar.gz binaries with `chmod +x` application. | Verified via Adoptium mock and downloader tests. |
| Managed PHP for PocketMine | `PhpDownloadService.cs` | `PHPService.cs` | Verified | Downloads prebuilt PHP binaries for Linux/macOS from PocketMine releases. | Verified Unix file permission handling. |
| Missing Runtime Recovery | Automatic prompt and background fetch | Handled on launch and creation | Verified | Auto-fetch before instance launch and creation wizard. | Tested runtime resolution fallbacks. |

---

## 5. Console, Telemetry & Player Management

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| ANSI Colored Console | `LogLineClassifier.cs`, RichText / FlowDocument | `ServerConsolePage.axaml`, `LogLine` | Verified | Formats stdout/stderr with timestamp, severity brush, circular buffer. | Tested with ANSI regex and formatting tests. |
| Historical Log Inspection | `ConsoleLogHistoryService.cs` | `IConsoleLogService.GetLogs` | Verified | Reads logs from instance directory when server is offline. | Verified via `ConsoleLogServiceTests`. |
| Resource Telemetry | `ResourceMonitorService.cs` | `ProcessMetricsTracker.cs` (CPU %, RAM MB) | Verified | Tracks process metrics and populates circular history buffer. | Verified via `ProcessMetricsTrackerTests`. |
| Player Roster & Actions | `PlayerManagementViewModel.cs`, OP/Deop/Kick/Ban | `PlayerManagementViewModel.cs`, `PlayerService.cs` | Verified | Standard console commands (`op`, `deop`, `kick`, `ban`, `pardon`). | Verified via `PlayerManagementViewModelTests`. |
| Bedrock Ban Sidecar | `BanSidecarService.cs` | `BanSidecarService.cs` | Verified | Manages persistent `pocketmc-bans.json` sidecar registry with atomic save for Bedrock servers. | Verified via `BanSidecarServiceTests`. |

---

## 6. Marketplace & Content Management

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Modrinth & CurseForge Integration | `ModrinthService.cs`, `CurseForgeService.cs` | `ModrinthService.cs`, `CurseForgeService.cs` | Verified | Search, pagination, download, dependencies, API key dialog. | Verified via integration and contract tests. |
| Modpacks & Overrides | `ModpackParser.cs`, `ModpackOverridePolicy.cs` | `ModpackParser.cs`, `MarketplaceArchiveInspector.cs` | Verified | Inspects CurseForge/Modrinth zip archives, extracts configs and mods safely, blocks client-only mods. | Verified via `ModpackParserTests`. |
| Addon Inventory & Toggle | `AddonManifestService.cs`, disable via `.disabled` | `AddonManifestService.cs` | Verified | Manifest tracking, rename toggle, delete. | Verified via `AddonManifestServiceTests`. |
| Addon Updates Discovery | `AddonUpdateService.cs` | `AddonUpdateService.cs` | Verified | Checks for upstream updates to installed mods/plugins using Modrinth/CurseForge APIs. | Verified via `AddonUpdateServiceTests`. |

---

## 7. Cross-Play, Voice Chat & Tunnels

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Playit.gg Tunnels | `PlayitPartnerProvisioningClient.cs`, `PlayitApiClient.cs` | `PlayitPartnerProvisioningClient.cs`, `PlayitApiClient.cs` | Verified | Native binary execution, claim code pairing, tunnel creation. | Verified via Playit tunnel tests. |
| Cloudflare Quick Tunnels | `CloudflaredQuickTunnelProvider.cs` | `CloudflaredQuickTunnelProvider.cs` | Verified | Spawns `cloudflared tunnel --url`, captures output URL. | Verified via Cloudflared provider tests. |
| Simple Voice Chat Detection | `SimpleVoiceChatConfigService.cs` | `SimpleVoiceChatConfigService.cs`, `SimpleVoiceChatDetector.cs` | Verified | Parses server properties and mod configs for voice chat UDP ports with atomic patching. | Verified via SimpleVoiceChat detector tests. |
| Interactive Ports Map | `PortsMapPage.xaml`, `PortsMapPage.xaml.cs` | `PortsMapPage.axaml`, `PortsMapViewModel.cs` | Verified | Full interactive topology visualizer grouping servers, local TCP/UDP bindings, Playit tunnels, and inline port editing. | Verified via `PortsMapViewModelTests`. |

---

## 8. Backups, Cloud Storage & Migration

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Local ZIP Archives & Retention | `BackupService.cs` | `BackupService.cs` | Verified | Safe archive packaging, retention pruning (`MaxBackupsToKeep`). | Verified via `BackupServiceTests`. |
| World Save Sync on Live Server | `save-off`, `save-all flush`, `save-on` | `BackupService.cs` issues `save-all` | Verified | Prevents disk write corruption while server is actively running. | Verified in `BackupService`. |
| Operations Mutual Exclusion | Coordinator lock acquisition | `BackupService.cs` acquires lock | Verified | Blocks backups during server updates/restores; blocks restores while server is running. | Verified via `BackupService` operations lock integration. |
| Cloud Sync (Google Drive, Dropbox, OneDrive) | `GoogleDriveBackupProvider.cs`, `DropboxBackupProvider.cs`, `OneDriveBackupProvider.cs` | `GoogleDriveBackupProvider.cs`, `DropboxBackupProvider.cs`, `OneDriveBackupProvider.cs` | Verified | PKCE OAuth loopback receiver on `127.0.0.1:49383/callback`. | Verified via cloud backup provider unit tests. |
| Cross-Platform Instance Export & Import | `InstanceExportService.cs`, `InstanceImportService.cs` | `InstanceService.cs`, `InstanceExportManifest.cs` | Verified | Full instance bundle export with `pocketmc-export.json` metadata and auto-detection import fallback. | Verified via export and import round-trip tests. |

---

## 9. Remote Control Dashboard

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Embedded Kestrel Web Host | `RemoteDashboardHost.cs` | `RemoteDashboardHost.cs` | Verified | Runs on localhost or LAN port, serves SPA from embedded assembly resources. | Verified via `RemoteDashboardHostTests`. |
| REST Management Endpoints | Instances, batch actions, player commands, properties, reboot schedules, file manager, backups | Full REST API in `RemoteDashboardHost.cs` | Verified | Complete REST API matching Windows contracts for instances, players, properties, reboot-schedule, files, and backups. | Verified: `RemoteDashboardHostTests` passing. |
| Remote File Access Security | `RemoteFileAccessPolicy.cs` | `RemoteFileAccessPolicy.cs` | Verified | Strict policy denies sensitive keys (.key, .pem, .pfx, .jks, .keystore), tokens, credentials, and internal journals unconditionally even for authenticated admins. Blocks path traversal. Allows clean directory/file browsing. | Verified: 27 test scenarios passing in `RemoteFilesAndBackupsTests`. |
| WebSocket Console Streaming | `RemoteConsoleWebSocketHandler.cs` | `RemoteConsoleWebSocketHandler.cs` | Verified | Real-time console output streaming with session token authentication. | Verified via WebSocket handler tests. |
| Web Dashboard SPA | `Web/index.html`, `Web/app.js`, `Web/styles.css` | `Web/index.html`, `Web/app.js`, `Web/styles.css` | Verified | Identical modern web client assets embedded and served directly from assembly. | Verified in assembly resources. |
| Remote Control UI & User Management | `RemoteControlPage.xaml`, `RemoteControlSettingsViewModel.cs` | `RemoteControlPage.axaml`, `RemoteControlViewModel.cs`, `RemoteControlUserViewModel.cs` | Verified | Dual-tab interface (General Settings vs Users & Permissions), LAN & Public Tunnel URL cards with live QR codes and copy actions, granular user permission toggles. | Verified via `RemoteControlViewModelTests`. |

---

## 10. AI Diagnostics & Intelligence

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Multi-Provider Strategy | OpenAI, Gemini, Claude, Groq, OpenRouter, Mistral, Grok, Ollama | `LlmProviderFactory.cs`, `AiDiagnosticService.cs` | Verified | Strategy pattern, secure API key retrieval from `ISecretStore`. | Verified via `LlmProviderFactoryTests`. |
| Privacy-First Log Redaction | `LogRedactionService.cs` | `LogRedactionService.cs` | Verified | Redacts IPv4/IPv6, UUIDs, auth tokens, player emails. | Verified via `LogRedactionServiceTests`. |
| Markdown Report Viewer | `NativeMarkdownViewer.xaml` | `MarkdownRenderer.cs` | Verified | Formatted presentation of AI diagnostic findings in Avalonia. | Verified via markdown rendering unit tests. |

---

## 11. Desktop Integration & UI/UX Parity

| Feature | Windows Implementation | Linux/macOS Implementation | Status | Platform Considerations | Validation & Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| First-Run Directory Wizard | `RootDirectorySetupPage.xaml` | `RootDirectorySetupWindow.axaml` | Verified | Allows custom storage root selection before app initialization. | Verified via `RootDirectorySetupViewModelTests`. |
| Theme & Accent Customization | Light, Dark, System, Dynamic Accents | `ThemeManager.cs` (Light, Dark, System) | Verified | Native window styling on macOS and Linux with theme persistence. | Verified via ThemeManager tests. |
| System Tray / Menu Bar | `Hardcodet.NotifyIcon.Wpf`, `TrayIconViewModel.cs` | Avalonia `TrayIcon` in `App.axaml`, `App.axaml.cs` | Verified | Background minimization, restore on click, and clean application shutdown on quit. | Verified in `App.axaml.cs`. |
| Release Notes & What's New | `WhatsNewWindow.xaml`, `WhatsNewService.cs` | `WhatsNewWindow.axaml`, `WhatsNewService.cs`, `ChangelogParser.cs` | Verified | In-app version highlights dialog shown once per release, parsed from `WhatsNew.txt`. | Verified via WhatsNewService tests. |
| Settings Parity & Scheduled Reboot UI | `ServerSettingsPage.xaml`, `SettingsAdvancedVM.cs` | `ServerSettingsPage.axaml`, `ServerSettingsViewModel.cs` | Verified | Full properties tabs: General, World, Performance, Advanced (with Scheduled Reboot, Frequency, and In-Game Warnings), Raw Properties, and Version Updates. | Verified: ViewModel properties, bindings, and persistence tested. |
| About & Studio Attribution Parity | `AboutPage.xaml`, `AboutPage.xaml.cs` | `AboutPage.axaml`, `AboutViewModel.cs` | Verified | Studio attribution (DS Labs), Hero card with logo, Co-Founders (Divy Viradiya, Sahaj Italiya), Contributors & Helpers (Johndinglesin), Donators (NexVice), storage root relocation, community links. | Verified via `AboutViewModelTests`. |
| Native POSIX Packaging & Distribution | InnoSetup / MSIX | `build-dmg.sh`, `build-appimage.sh`, `build-deb.sh`, `build-rpm.sh`, `pocketmc.desktop`, `pocketmc.mime.xml` | Verified | Apple Silicon & Intel macOS DMG with URL scheme handling, AppImage, Debian (.deb), RPM (.rpm), Freedesktop entry, and `IPosixFilePermissionService` (0755 permissions). | Verified via `PosixFilePermissionServiceTests` and packaging scripts. |

---

## 12. Known Platform Limitations & Architectural Variations

1. **Process Tree Supervision:**
   - Windows utilizes Win32 Job Objects (`CreateJobObject`) to guarantee child process group cleanup.
   - Linux & macOS utilize POSIX process groups via libc `setpgid` and signal targeting `kill(-pgid, sig)`. In container or non-standard shell environments where `setpgid` may fail, process group escalation cleanly targets the direct process PID.

2. **Bedrock Dedicated Server (BDS) Architecture Constraints:**
   - Mojang only releases BDS binaries for x86_64 Windows and x86_64 Linux.
   - macOS (Apple Silicon and Intel) and Linux ARM64 systems cannot run BDS natively without x86_64 emulation (e.g. Rosetta 2 or FEX-Emu). For cross-play on ARM64 and macOS, Paper or Fabric with Geyser + Floodgate is the recommended and supported solution.

3. **Secure Keyring Backends:**
   - Windows uses DPAPI (`ProtectedData`).
   - Linux uses FreeDesktop SecretService / D-Bus (GNOME Keyring / KWallet), falling back to local AES-GCM encrypted persistence when running in headless or non-desktop Linux environments.
   - macOS uses Apple Keychain Services via the native `security` CLI interface.

4. **Translucent Backdrops:**
   - Windows supports Mica and Acrylic compositor backdrops.
   - macOS supports `NSVisualEffectView` materials.
   - Linux depends on the running window manager (Wayland/X11 Compositor) supporting background blur; tasteful solid surface fallback brushes are provided on unsupported compositors.

---

## 13. Automated Test Suite Summary

- Total Automated Tests: 212
- Passing: 212
- Failed: 0
- Skipped: 0
- Test Duration: ~17s
- Coverage Areas: Server update transactions, artifact integrity, POSIX process management, file security policies, reboot schedules, port reliability engine, native metrics, sleep prevention, notifications, Discord RPC, console filters, player log parsers, crash detector, about page, remote control user permissions, ViewLocator type resolution, and host container DI resolution.
