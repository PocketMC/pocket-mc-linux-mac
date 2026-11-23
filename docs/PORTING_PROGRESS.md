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

---

## Phase 12: Controlled Navigation, Shell & Visual Aesthetic Parity (Completed & Verified)
- [x] Implemented `ControlledNavigationStack.cs` providing bidirectional navigation history (`NavigateTo`, `GoBack`, `CanGoBack`, `ClearHistory`).
- [x] Implemented `WallpaperBackdropService.cs` providing translucent frosted surfaces over custom or default wallpaper (`default_wallpaper.png`, `dark_dirt.png`) with dynamic Skia blurred-wallpaper fallback for non-composited X11 and Wayland environments.
- [x] Implemented `AccentColorService.cs` extracting system accent colors with runtime resource injection.
- [x] Created `AdaptiveGridPanel.cs` for responsive card grids adapting smoothly to window resizes.
- [x] Created `SkeletonStyles.axaml` providing animated shimmer skeleton loading placeholders.
- [x] Implemented `SplashWindow.axaml` startup splash screen with high-resolution branding.
- [x] Standardized modal dialog architecture (`AppDialogWindow.axaml`, `PasswordPromptDialogWindow.axaml`, `ProgressDialogWindow.axaml`) via `AvaloniaDialogService.cs`.
- [x] Automated tests verified in `ControlledNavigationStackTests`, `WallpaperBackdropServiceTests`, `AccentColorServiceTests`, and `DialogServiceTests`.

---

## Phase 13: Settings Decomposition & Backup / Restore System (Completed & Verified)
- [x] Decomposed `AppSettingsViewModel.cs` into modular category sections matching Windows: Appearance, Java, Network, Storage, Diagnostics, Updates, and Advanced.
- [x] Implemented `SettingsBackupService.cs` supporting JSON manifest export/import of full application configuration with validation and rollback.
- [x] Automated tests verified in `AppSettingsViewModelTests` and `SettingsBackupServiceTests`.

---

## Phase 14: Dashboard Decomposition & Instance Management Parity (Completed & Verified)
- [x] Decomposed monolithic dashboard into cohesive, modular ViewModels: `DashboardActionsViewModel`, `DashboardInstanceListViewModel`, `DashboardMetricsViewModel`, and `InstanceCardViewModel`.
- [x] Implemented `NativeMetricsProvider.cs` leveraging `/proc/[pid]/stat` and `/proc/meminfo` on Linux, and `proc_pidinfo` with `ps` on macOS for zero-overhead CPU and memory sampling.
- [x] Automated tests verified in `DashboardDecompositionTests` and `NativeMetricsProviderTests`.

---

## Phase 15: Server Settings Decomposition & Advanced Configuration (Completed & Verified)
- [x] Decomposed `ServerSettingsViewModel.cs` into 9 focused tabs matching Windows: General, Addons, World, Backups, Performance, Bedrock, Updates, Summaries, and Advanced.
- [x] Implemented `MinecraftMotdConverter.cs` for real-time legacy and formatting code previews (`§` and `&`).
- [x] Implemented `ImageCropPage.axaml` and `ImageProcessingService.cs` for SkiaSharp-powered 64x64 icon cropping and resizing.
- [x] Implemented `SummaryStorageService.cs` for per-instance AI session summary history storage.
- [x] Automated tests verified in `ServerSettingsDecompositionTests`.

---

## Phase 16: Networking, Port Reliability & Process-Aware Conflict Resolution (Completed & Verified)
- [x] Implemented `PortReliabilityEngine.cs` supporting TCP and UDP socket probing, timeout handling, conflict classification, and remediation recommendations.
- [x] Implemented `PosixPortOwnerResolver.cs` utilizing `ss -tlpn`, `ss -ulpn`, and `lsof -i` to identify conflicting process names and PIDs on Linux and macOS.
- [x] Implemented `InstanceTunnelOrchestrator.cs` supporting concurrent Playit tunnels for Minecraft Java (TCP), Bedrock (UDP), and Simple Voice Chat (UDP).
- [x] Automated tests verified in `PortReliabilityEngineTests` and `TunnelManagerTests`.

---

## Phase 17: Console, Players, Crash Diagnostics & Remote Control UI (Completed & Verified)
- [x] Implemented `LogLineClassifier.cs` and `ConsoleLogFilter.cs` supporting structured filtering (All, Info, Warning, Error, Chat) and search highlights in `ServerConsoleViewModel.cs`.
- [x] Implemented `PlayerListParser.cs` and `PlayerGamemodeLogParser.cs` for automatic gamemode and player tracking in `PlayerViewModel.cs`.
- [x] Implemented `ServerCrashDetector.cs` and `ServerCrashDialogWindow.axaml` with automated crash log analysis and suggested remedies.
- [x] Implemented `OllamaModelManagerDialog.axaml` for listing, pulling, and switching local Ollama AI models.
- [x] Implemented `AboutPage.axaml` and `AboutViewModel.cs` matching Windows: Hero Card, Studio Attribution ("DS Labs"), Team and Contributor cards with circular avatars (`avatar_*.png`), and storage directory relocation.
- [x] Implemented modern dual-tab UI in `RemoteControlPage.axaml` with LAN/Public Tunnel URL cards, QR code generation, and granular user permission checkboxes.
- [x] Automated tests verified in `ConsoleLogFilterTests`, `PlayerParserTests`, `ServerCrashDetectorTests`, `AboutViewModelTests`, and `RemoteControlViewModelTests`.

---

## Phase 18: POSIX Hardening, Packaging & Release Distribution (Completed & Verified)
- [x] Implemented `IPosixFilePermissionService.cs` and `PosixFilePermissionService.cs` enforcing `0755` executable permissions via `File.SetUnixFileMode` with `chmod +x` fallback.
- [x] Hardened process group management with libc `setpgid` and signal escalation (`SIGTERM` -> `SIGKILL`) across all runners and tunnels.
- [x] Built native Linux packaging scripts: AppImage (`scripts/build-appimage.sh`), Debian package (`scripts/build-deb.sh`), RPM spec (`scripts/build-rpm.sh`), desktop entry (`scripts/pocketmc.desktop`), MIME types (`scripts/pocketmc.mime.xml`), and AppStream metadata (`scripts/io.github.pocketmc.app.metainfo.xml`).
- [x] Built native macOS packaging script: DMG installer (`scripts/build-dmg.sh`) with `pocketmc://` custom URL scheme registration.
- [x] Enhanced GitHub Actions release pipeline (`.github/workflows/release.yml`) to compile and package artifacts for Linux (`.tar.gz`, `.deb`, `.AppImage`) and macOS (`.dmg`).
- [x] Automated tests verified in `PosixFilePermissionServiceTests`.

---

## Phase 19: High-Fidelity UI/UX Parity & Linux Runtime Hardening (Completed & Verified)
- [x] **Design Tokens & Theme Resources**: Registered all Windows WPF-UI theme tokens (`CardBackgroundFillColorDefaultBrush`, `ControlFillColorSecondaryBrush`, `TextFillColorPrimaryBrush`, `AccentFillColorDefaultBrush`, etc.) across Light and Dark themes in `App.axaml`.
- [x] **Shell & Navigation Alignment**: Updated `MainWindow.axaml` to 240px navigation rail, collapsible top update banner with restart button, dynamic TitleBar server context pill, and Lucide icon parity.
- [x] **Instance Cards & Dashboard**: Added 4-card pulsing skeleton loader, centered empty state with creation button, version badges, cross-play and voice chat indicators, 3-column metadata grid (`Last Played`, `Created`, `Platform`), metrics grid (`CPU`, `RAM`, `PLAYERS`), running IP pills, and 2x2 action buttons.
- [x] **New Instance Wizard**: Replicated top header with Back button, "Import Modpack" (with BETA badge), and "Import Instance". Aligned sticky footer action bar with EULA checkbox and download progress.
- [x] **Server Console**: Cascadia Code monospace terminal, action toolbar with Auto-scroll, Copy, regex search toggle (`.*`), level filters (`Chat`, `Info`, `Warn`, `Error`, `Sys`), and high-contrast "AI Summary" button (`Button.ai-action`).
- [x] **News & Announcements Page**: Implemented `NewsPage.axaml`, `NewsViewModel.cs`, and `NewsService.cs` matching Windows 2-column layout (300px list + article detail surface), unread indicators, priority badges, and `Ctrl+7` shortcut.
- [x] **Linux VM Runtime Stabilization**:
  - Eliminated ambiguous `DashboardViewModel` DI constructor crash during first-run storage root directory setup.
  - Resolved `InvalidCastException` on two-way compiled bindings by enforcing `control.DataContext = data` in `ViewLocator.cs` and adding ViewModel constructors to views.
  - Centralized all view and detail navigation through `IAppNavigationService`.
- [x] **Automated Regression Suite**: 214 automated tests passing across core, infrastructure, platform, remote control, and UI hosting/navigation.

