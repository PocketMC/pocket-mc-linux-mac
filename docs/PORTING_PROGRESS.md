# PocketMC Linux & macOS Porting Progress

This log tracks architectural decisions, ported capabilities, and verification states for bringing PocketMC Linux & macOS to full feature and experience parity with PocketMC Windows.

---

## Phase 1: Audit and Foundations (Completed & Verified)
- [x] Initialized parity matrix in `docs/WINDOWS_PARITY_MATRIX.md`.
- [x] Indexed both Windows (`pocket-mc-windows`) and Linux/macOS (`pocket-mc-linux-mac`) repositories in Codebase Memory knowledge graph.
- [x] Identified high-priority gap: Server Version Update system (`InstanceVersionTargetService`, `InstanceUpdatePlanner`, `InstanceArtifactStager`, `InstanceUpdateApplier`, `InstanceFileTransaction`, `InstanceOperationCoordinator`).
- [x] Identified Remote Control dashboard parity gaps: File manager endpoints, properties editor, batch actions, player command extensions, and updated web assets.
- [x] Stabilized package dependencies: Aligned `Microsoft.Extensions.Hosting` to LTS `8.0.1` in `PocketMC.App` and `PocketMC.Infrastructure` for clean `.NET 8` compatibility.
- [x] Resolved pre-existing test suite failures in `PortProber`, `TunnelProcessGroupManager`, and `PlayerManagementViewModel`.

---

## Phase 2: Server Version Updates System (Completed & Verified)
- [x] Implemented `InstanceFileTransaction.cs` providing transactional file staging and journal rollback via `.pocketmc-file-transaction.json`.
- [x] Implemented `InstanceOperationCoordinator.cs` providing thread-safe mutual exclusion between server execution, backups, and updates.
- [x] Implemented `InstanceVersionTargetService.cs` supporting Mojang, PaperMC, Fabric, Forge, NeoForge, Bedrock, and PocketMine version queries.
- [x] Implemented `InstanceUpdatePlanner.cs` and `InstanceArtifactStager.cs` for version target viability and non-destructive staging.
- [x] Implemented `InstanceUpdateApplier.cs` with rollback preservation of worlds, properties, and player data.
- [x] Implemented `InstanceUpdateService.cs` full update orchestration.
- [x] Automated tests verified in `PocketMC.Tests.InstanceUpdateTests` (6 tests passing).

---

## Phase 3: Geyser & Floodgate Provisioning & Crossplay (Completed & Verified)
- [x] Implemented `DownloaderService.cs` supporting buffered downloads, partial identity checks, and SHA-256 validation.
- [x] Implemented `GeyserDetector.cs` for automated inspection of Geyser and Floodgate jars in plugins directory.
- [x] Implemented `GeyserProvisioningService.cs` for automated downloading and dynamic UDP port patching in `config.yml`.
- [x] Updated `NewInstanceViewModel.cs` with Geyser cross-play toggle and automated setup.
- [x] Automated tests verified in `GeyserDetectorTests` and `GeyserProvisioningServiceTests`.

---

## Phase 4: Simple Voice Chat Configuration & Detection (Completed & Verified)
- [x] Implemented `SimpleVoiceChatDetector.cs` for discovering Voice Chat jar presence across Spigot, Paper, Fabric, and Forge.
- [x] Implemented `SimpleVoiceChatConfigService.cs` with atomic property parsing and patching while preserving comments.
- [x] Automated tests verified in `SimpleVoiceChatConfigServiceTests`.

---

## Phase 5: Bedrock Ban Sidecar Service (Completed & Verified)
- [x] Implemented `BanSidecarService.cs` for atomic management of `pocketmc-bans.json` sidecar for Bedrock Dedicated Server.
- [x] Automated tests verified in `BanSidecarServiceTests`.

---

## Phase 6: Marketplace Parity & Modpack Ingestion (Completed & Verified)
- [x] Implemented `MarketplaceArchiveInspector.cs` for inspecting mod environment requirements and blocking client-only mods.
- [x] Implemented `MarketplaceFileInstaller.cs` for safe archive extraction.
- [x] Implemented `ModpackParser.cs` for CurseForge `manifest.json` and Modrinth `modrinth.index.json` ingestion.
- [x] Implemented `AddonUpdateService.cs` for discovering upstream mod updates.
- [x] Automated tests verified in `MarketplaceArchiveInspectorTests`.

---

## Phase 7: Remote Control Endpoints & Host Parity (Completed & Verified)
- [x] Embedded full SPA web client (`index.html`, `app.js`, `styles.css`, icons) from Windows.
- [x] Implemented Remote Control DTOs (`RemoteFileItemDto`, `RemoteBackupDto`, `RemoteInstanceDto`, `RemotePlayerDto`, `RemoteServerPropertiesDto`, etc.).
- [x] Implemented `RemoteFileAccessPolicy.cs` with symlink / reparse-point blocking, DOS 8.3 alias prevention, and token protection.
- [x] Implemented `RemoteInstanceControlService.cs`, `RemotePlayerActionService.cs`, and `RemoteStatusService.cs`.
- [x] Implemented `ServerPropertiesHelper.cs` for lossless server properties read/write.
- [x] Implemented full REST endpoints in `RemoteDashboardHost.cs` (`/api/status`, `/api/instances`, `/api/instances/{id}/status`, `/api/instances/batch`, `/start`, `/stop`, `/restart`, `/console/history`, `/console/command`, `/players`, `/players/{name}/{action}`, `/properties`, `/reboot-schedule`, `/addons`, `/addons/uninstall`, `/files`, `/files/content`, `/files/upload`, `/backups`).
- [x] Automated tests verified in `RemoteFilesAndBackupsTests` (13 tests passing), `RemoteControlServicesTests` (5 tests passing), and `RemoteDashboardHostTests` (live Kestrel integration test passing).

---

## Phase 8: Instance Export & Import Parity (Completed & Verified)
- [x] Standardized `InstanceExportManifest.cs` (`pocketmc-export.json`) for cross-platform instance bundle exchange between Windows, Linux, and macOS.
- [x] Upgraded `InstanceService.ExportInstanceAsync` to embed manifest while excluding locks, staging caches, and sockets.
- [x] Upgraded `InstanceService.ImportInstanceAsync` with manifest metadata parsing and heuristic fallback for standalone world ZIPs.
- [x] Automated tests verified in `InstanceServiceTests.ExportAndImport_SucceedsCleanly`.

---

## Phase 9: Desktop Ports Map Visualizer (Completed & Verified)
- [x] Implemented `PortsMapViewModel.cs` discovering primary game ports (TCP/UDP), RCON (TCP), Query (UDP), Geyser Bedrock (UDP), Simple Voice Chat (UDP), and active Playit tunnels.
- [x] Implemented offline port editing and saving with input validation (1-65535) and atomic `server.properties` updates.
- [x] Implemented `PortsMapPage.axaml` and `PortsMapPage.axaml.cs` with interactive 3-node topology layout and clipboard copy.
- [x] Added "Interactive Ports Map" navigation button to `TunnelPage.axaml`.
- [x] Automated tests verified in `PortsMapViewModelTests` (2 tests passing).

---

## Phase 10: System Tray & Platform Lifecycle (Completed & Verified)
- [x] Implemented `IProcessRunner.StopAllAsync()` and `GetRunningSlugs()` for parallel graceful server termination.
- [x] Implemented Avalonia `TrayIcon` in `App.axaml` with native context menu ("Open PocketMC", "Quit PocketMC") and click-to-restore.
- [x] Wired application lifecycle shutdown hooks in `App.axaml.cs` (`desktop.Exit`) to stop all running servers and background tunnels cleanly.
- [x] Automated tests verified in `ProcessRunnerTests.StopAllAsync_WhenNoInstancesRunning_CompletesCleanly`.

---

## Phase 11: Release Notes & What's New Dialog (Completed & Verified)
- [x] Implemented `ChangelogParser.cs` supporting structured sections (`[NEW FEATURES]`, `[IMPROVEMENTS]`, `[FIXES & STABILITY]`) and bullet items.
- [x] Implemented `WhatsNewService.cs` with version comparison and `LastSeenChangelogVersion` persistence in `Settings.cs`.
- [x] Implemented `WhatsNewWindow.axaml` and `WhatsNewWindow.axaml.cs` modal dialog with external GitHub releases link.
- [x] Created `WhatsNew.txt` detailing v1.0.3 Linux/macOS release highlights.
- [x] Wired automatic display on launch when a newer release is detected in `App.axaml.cs`.
- [x] Automated tests verified in `ChangelogParserTests` and `WhatsNewServiceTests` (4 tests passing).
