# PocketMC Linux & macOS - Full Parity Execution Plan

## 1. Overview & Objectives

This document establishes the exhaustive, feature-by-feature, architectural, and visual execution plan for bringing PocketMC Linux & macOS (`pocket-mc-linux-mac`) to 100% production-ready parity with the authoritative Windows implementation (`pocket-mc-windows`).

The goal is functional, experiential, and engineering parity:
- Zero stubs, zero fake implementations, zero unhandled errors.
- Dark-first aesthetic, dynamic accents, frosted surfaces over wallpapers with Skia blurred-wallpaper fallbacks for non-composited X11 and Wayland environments.
- Strict architecture standards: Clean Architecture separation, config single-source-of-truth, atomic disk writes, path safety, and HTTP resilience.
- Native Linux (systemd, D-Bus, XDG, `/proc`, `ss`/`lsof`) and macOS (Apple Silicon + Intel, Keychain, `caffeinate`, `/Library`, `ps`/`proc_pidinfo`) ergonomics.

---

## 2. Inventory & Parity Status Matrix

### Legend
- **Done:** Fully implemented, verified against platform requirements with automated tests.
- **Partial:** Basic implementation exists, but lacks advanced Windows workflows, edge-case hardening, or modular structure.
- **Diverged:** Implemented using a different architecture that must be aligned or refactored for parity.
- **Missing:** Not yet present in the Linux/macOS target repository.

---

### Layer 1: Domain & Core Models

| Windows Component | Target File | Status | Notes & Scope |
| :--- | :--- | :--- | :--- |
| `PocketMC.Domain/Models/InstanceMetadata.cs` | `PocketMC.Core/Models/ServerInstance.cs` | Done | Full instance metadata with persistence and serialization. |
| `PocketMC.Domain/Models/InstanceUpdateModels.cs` | `PocketMC.Core/Models/InstanceUpdateModels.cs` | Done | Target versions, update plans, and execution results. |
| `PocketMC.Domain/Models/RebootScheduleSettings.cs` | `PocketMC.Core/Models/RebootScheduleSettings.cs` | Done | Daily and interval reboot schedule parameters. |
| `PocketMC.Domain/Models/SessionSummary.cs` | `PocketMC.Core/Models/SessionSummary.cs` | Missing | Per-server AI session summary storage model. |
| `PocketMC.Domain/Models/OllamaModels.cs` | `PocketMC.Core/Models/OllamaModels.cs` | Missing | Local Ollama model catalog, pull status, and tags. |
| `PocketMC.Domain/Models/Port*.cs` (PortDiagnostics, PortLease, PortEngine, PortProtocol) | `PocketMC.Core/Models/PortReliabilityModels.cs` | Missing | Port reliability domain contracts and diagnostic snapshot models. |
| `PocketMC.Domain/Models/BedrockPackModels.cs` | `PocketMC.Core/Models/BedrockPackModels.cs` | Missing | Behavior and resource pack manifest models for Bedrock instances. |
| `PocketMC.Domain/Models/SettingsBackupModels.cs` | `PocketMC.Core/Models/SettingsBackupModels.cs` | Missing | Full app configuration export/import manifest models. |
| `PocketMC.Domain/Models/CrashAnalysisResult.cs` | `PocketMC.Core/Models/CrashAnalysisResult.cs` | Missing | Structured crash categorization and recommendation model. |
| `PocketMC.Domain/Models/GlobalResourceSummary.cs` | `PocketMC.Core/Models/GlobalResourceSummary.cs` | Missing | Aggregated host CPU, memory, and active instance statistics. |
| `PocketMC.Domain/Security/PathSafety.cs` | `PocketMC.Core/Security/PathSafety.cs` | Done | Path traversal, link resolution, and directory boundary enforcement. |
| `PocketMC.Domain/Storage/SafeZipExtractor.cs` | `PocketMC.Core/Storage/SafeZipExtractor.cs` | Done | Zip-Slip protected archive extraction. |
| `PocketMC.Domain/Storage/MarketplaceFileNameSanitizer.cs` | `PocketMC.Core/Storage/MarketplaceFileNameSanitizer.cs` | Done | File name sanitation for downloaded addons and modpacks. |

---

### Layer 2: Application Use Cases & Infrastructure Services

| Windows Component | Target File | Status | Notes & Scope |
| :--- | :--- | :--- | :--- |
| `InstanceVersionTargetService.cs` | `PocketMC.Infrastructure/Services/InstanceVersionTargetService.cs` | Done | Version target discovery across all 7 engine loaders. |
| `InstanceUpdatePlanner.cs` | `PocketMC.Infrastructure/Services/InstanceUpdatePlanner.cs` | Done | Viability planning and installer resolution. |
| `InstanceArtifactStager.cs` | `PocketMC.Infrastructure/Services/InstanceArtifactStager.cs` | Done | Non-destructive download staging with SHA1/SHA256 checksum validation. |
| `InstanceUpdateApplier.cs` | `PocketMC.Infrastructure/Services/InstanceUpdateApplier.cs` | Done | Atomic two-phase update applier with journal rollback. |
| `InstanceFileTransaction.cs` | `PocketMC.Infrastructure/Services/InstanceFileTransaction.cs` | Done | Rollback journal with uncommitted preservation. |
| `InstanceOperationCoordinator.cs` | `PocketMC.Infrastructure/Services/InstanceOperationCoordinator.cs` | Done | Concurrent semaphore mutual exclusion. |
| `RebootScheduleService.cs` | `PocketMC.Infrastructure/Services/RebootScheduleService.cs` | Done | Schedule validation and persistence. |
| `ServerRebootSchedulerService.cs` | `PocketMC.Infrastructure/Services/ServerRebootSchedulerService.cs` | Done | 30s background supervisor, in-game warnings, and graceful restart. |
| `PortCheckRequest.cs`, `PortProbeService.cs`, `PortLeaseRegistry.cs` | `PocketMC.Infrastructure/Networking/PortReliabilityEngine.cs` | Partial | Basic `PortProber` exists; needs full Windows port reliability engine porting. |
| `InstanceTunnelOrchestrator.cs` | `PocketMC.Infrastructure/Services/InstanceTunnelOrchestrator.cs` | Missing | Java, Bedrock, and Simple Voice Chat UDP multi-tunnel orchestration. |
| `SessionSummarizationService.cs` | `PocketMC.Infrastructure/Services/SessionSummarizationService.cs` | Missing | AI log preprocessor, summarization, and storage service. |
| `OllamaService.cs` | `PocketMC.Infrastructure/Services/OllamaService.cs` | Missing | Local Ollama API integration (pull, list, delete, test connection). |
| `WorldManager.cs`, `WorldInventory.cs` | `PocketMC.Infrastructure/Services/WorldManager.cs` | Missing | Multi-world management, switching, backups, and dimension cleanup. |
| `AddonIconService.cs` | `PocketMC.Infrastructure/Services/AddonIconService.cs` | Missing | Addon icon disk caching and thumbnail management. |
| `CloudBackupService.cs` | `PocketMC.Infrastructure/Services/CloudBackupService.cs` | Done | Google Drive, Dropbox, OneDrive OAuth PKCE and synchronization. |
| `BanSidecarService.cs` | `PocketMC.Infrastructure/Services/BanSidecarService.cs` | Done | Bedrock dedicated server ban registry sidecar. |
| `GeyserProvisioningService.cs` | `PocketMC.Infrastructure/Services/GeyserProvisioningService.cs` | Done | Automated Geyser and Floodgate download and UDP port patching. |
| `SimpleVoiceChatConfigService.cs` | `PocketMC.Infrastructure/Services/SimpleVoiceChatConfigService.cs` | Done | Simple Voice Chat port auto-detection and atomic configuration. |

---

### Layer 3: Platform & OS Integration (`PocketMC.Platform`)

| Windows Component | Target File | Status | Notes & Scope |
| :--- | :--- | :--- | :--- |
| `SafeSecretStore.cs` (DPAPI) | `PocketMC.Platform/Services/SecretStoreFactory.cs` | Done | Linux SecretService / macOS Keychain / AES-GCM fallback. |
| Sleep Prevention | `PocketMC.Platform/Lifecycle/SleepPreventionService.cs` | Missing | Linux `systemd-inhibit` / D-Bus; macOS `caffeinate` / IOKit assertion. |
| Single Instance & URL Protocol | `PocketMC.Platform/Lifecycle/SingleInstanceService.cs` | Missing | Unix domain socket lock; `pocketmc://` scheme registration. |
| Native Notifications | `PocketMC.Platform/Notifications/NativeNotificationService.cs` | Missing | Linux `notify-send` / D-Bus; macOS UserNotifications / `osascript`. |
| Discord Rich Presence | `PocketMC.Platform/Integrations/DiscordRpcService.cs` | Missing | Cross-platform Discord RPC integration for active servers. |
| Native Metrics Sampling | `PocketMC.Platform/Metrics/NativeMetricsProvider.cs` | Missing | Linux `/proc/[pid]/stat`, `/proc/meminfo`; macOS `proc_pidinfo` and `ps`. |

---

### Layer 4: Shell, Navigation & Presentation (`PocketMC.App`)

| Windows Component | Target File | Status | Notes & Scope |
| :--- | :--- | :--- | :--- |
| `MainWindow.xaml` | `PocketMC.App/Views/MainWindow.axaml` | Partial | Needs titlebar context, ExtendClientArea macOS traffic-light insets, and full visual hierarchy. |
| `ShellViewModel.cs` | `PocketMC.App/ViewModels/MainWindowViewModel.cs` | Partial | Needs complete navigation history stack, active server badges, and keyboard shortcuts. |
| `WallpaperMicaService.cs` | `PocketMC.App/Services/WallpaperBackdropService.cs` | Missing | Wallpaper rendering with configurable Skia blur and fallback for non-composited environments. |
| `AccentColorService.cs` | `PocketMC.App/Services/AccentColorService.cs` | Missing | Extraction of system accent colors and runtime brush injection. |
| `AdaptiveGridPanel.cs` | `PocketMC.App/Controls/AdaptiveGridPanel.cs` | Missing | Responsive card layout panel adapting dynamically to window resize. |
| `SkeletonStyles.xaml` | `PocketMC.App/Styles/SkeletonStyles.axaml` | Missing | Shimmer skeleton loading placeholder styles. |
| `AppDialogWindow.xaml` | `PocketMC.App/Views/Dialogs/AppDialogWindow.axaml` | Missing | Unified themed modal dialog window. |
| `PasswordPromptDialogWindow.xaml` | `PocketMC.App/Views/Dialogs/PasswordPromptDialogWindow.axaml` | Missing | Themed password/secret entry dialog. |
| `ProgressDialogWindow.xaml` | `PocketMC.App/Views/Dialogs/ProgressDialogWindow.axaml` | Missing | Determinate and indeterminate progress dialog with cancel support. |
| `SplashWindow.xaml` | `PocketMC.App/Views/SplashWindow.axaml` | Missing | Startup splash window with `splash.png`. |
| `TrayIconViewModel.cs` | `PocketMC.App/ViewModels/TrayIconViewModel.cs` | Partial | Minimal tray exists; needs full menu actions and minimize-to-tray preference. |

---

### Layer 5: Feature Pages & ViewModels

| Windows Component | Target File | Status | Notes & Scope |
| :--- | :--- | :--- | :--- |
| `DashboardPage.xaml` | `PocketMC.App/Views/DashboardView.axaml` | Partial | Needs decomposition into modular sub-VMs and live metric graphs. |
| `DashboardActionsViewModel.cs` | `PocketMC.App/ViewModels/DashboardActionsViewModel.cs` | Missing | Modular actions VM for starting, stopping, and restarting instances. |
| `DashboardMetricsViewModel.cs` | `PocketMC.App/ViewModels/DashboardMetricsViewModel.cs` | Missing | Modular metrics VM tracking host CPU, memory, and network. |
| `InstanceCardViewModel.cs` | `PocketMC.App/ViewModels/InstanceCardViewModel.cs` | Missing | Rich instance card with status pill, quick actions, and tunnel badge. |
| `ServerSettingsPage.xaml` (9 sub-VMs) | `PocketMC.App/Views/ServerSettingsPage.axaml` | Partial | Currently a single monolithic file. Must be decomposed into 9 modular tabs. |
| `SettingsGeneralVM.cs` + `MinecraftMotdConverter.cs` | `PocketMC.App/ViewModels/Settings/SettingsGeneralViewModel.cs` | Partial | General properties + formatted Minecraft MOTD color preview. |
| `ImageCropPage.xaml` (SkiaSharp) | `PocketMC.App/Views/Settings/ImageCropPage.axaml` | Missing | 64x64 `server-icon.png` interactive cropping and scaling tool. |
| `SettingsAddonsVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsAddonsViewModel.cs` | Missing | Full addon inventory, toggle status, icon display, and update checks. |
| `SettingsWorldVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsWorldViewModel.cs` | Missing | Multi-world management, switching, seed inspection, and dimension resets. |
| `SettingsBackupsVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsBackupsViewModel.cs` | Missing | Unified local and cloud backup management for the instance. |
| `SettingsPerformanceVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsPerformanceViewModel.cs` | Missing | JVM memory sliders, GC presets (Aikar, ZGC, Shenandoah), and CPU thread allocation. |
| `SettingsBedrockVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsBedrockViewModel.cs` | Missing | BDS and PocketMine-MP configuration and pack management. |
| `SettingsVersionUpdatesVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsVersionUpdatesViewModel.cs` | Done | Integrated in settings; needs dedicated sub-VM binding. |
| `SettingsSummariesVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsSummariesViewModel.cs` | Missing | AI session summaries history for the instance. |
| `SettingsAdvancedVM.cs` | `PocketMC.App/ViewModels/Settings/SettingsAdvancedViewModel.cs` | Partial | Raw properties, custom JVM args, RCON, and scheduled maintenance. |
| `AppSettingsPage.xaml` | `PocketMC.App/Views/AppSettingsPage.axaml` | Missing | Global application settings page matching Windows. |
| `SettingsBackupDialogWindow.xaml` | `PocketMC.App/Views/Dialogs/SettingsBackupDialogWindow.axaml` | Missing | App configuration export/import dialog. |
| `InstanceExportPage.xaml`, `InstanceImportPage.xaml` | `PocketMC.App/Views/Instances/` | Missing | Dedicated import and export wizard pages with manifest metadata. |
| `PluginBrowserPage.xaml`, `MapBrowserPage.xaml` | `PocketMC.App/Views/Marketplace/` | Missing | Split marketplace into separate plugin and world map browser tabs. |
| `PortConflictWindow.xaml` | `PocketMC.App/Views/Networking/PortConflictWindow.axaml` | Missing | Interactive port conflict resolution window with owner detection. |
| `CreateTunnelDialog.xaml`, `PlayitConsoleWindow.xaml` | `PocketMC.App/Views/Tunnel/` | Missing | Tunnel creation dialog and live Playit process log console. |
| `OllamaModelManagerDialog.xaml` | `PocketMC.App/Views/Intelligence/OllamaModelManagerDialog.axaml` | Missing | Local Ollama model download and configuration manager. |
| `ServerCrashDialogWindow.xaml` | `PocketMC.App/Views/Instances/ServerCrashDialogWindow.axaml` | Missing | Crash diagnosis dialog with automated log inspection. |
| `NewsPage.xaml`, `NewsPopupWindow.xaml` | `PocketMC.App/Views/News/` | Missing | Community news feed, article reader, and popup notifications. |

---

## 3. Phased Execution Roadmap

### Phase 1: Shell, Navigation, Theming & Native Desktop Lifecycle
1. Import visual assets from Windows: `dark_dirt.png`, `default_wallpaper.png`, `splash.png`, contributor avatars.
2. Port `AdaptiveGridPanel.cs`, `SkeletonStyles.axaml`, and `ScrollViewerHelper.cs`.
3. Port `WallpaperBackdropService.cs` and `AccentColorService.cs` with Skia blurred-wallpaper fallback.
4. Implement unified dialog system: `AppDialogWindow.axaml`, `PasswordPromptDialogWindow.axaml`, and `ProgressDialogWindow.axaml`.
5. Implement `PocketMC.Platform` lifecycle services:
   - `SingleInstanceService`: Unix domain socket single-instance handoff and `pocketmc://` URL protocol handling.
   - `SleepPreventionService`: `systemd-inhibit` on Linux and `caffeinate` on macOS.
   - `NativeNotificationService`: Linux D-Bus and macOS `osascript`/NotificationCenter.
   - `DiscordRpcService`: Rich presence for running servers.
6. Upgrade `MainWindow.axaml` and `MainWindowViewModel.cs` with navigation stack, back-button handling, and titlebar context.

### Phase 2: Global App Settings & First-Run Setup Parity
1. Port `AppSettingsPage.axaml` and `AppSettingsViewModel.cs` covering Appearance, Wallpaper, Accent, Startup, Sleep Prevention, Remote Control, Cloud Backups, AI Providers, and Storage.
2. Port `SettingsBackupDialogWindow.axaml` for full configuration export and import.
3. Enhance `JavaManagementPage.axaml` to detect standard Linux and macOS JDK installation paths (`/usr/lib/jvm`, Homebrew, SDKMAN, `/Library/Java/JavaVirtualMachines`).
4. Upgrade `RootDirectorySetupWindow.axaml` with storage validation and custom directory selection.

### Phase 3: Dashboard, Lifecycle & Live Resource Monitoring
1. Decompose `DashboardViewModel` into modular components: `DashboardViewModel`, `DashboardActionsViewModel`, `DashboardInstanceListViewModel`, `DashboardMetricsViewModel`, and `InstanceCardViewModel`.
2. Port `NativeMetricsProvider.cs` using Linux `/proc/[pid]/stat` and macOS `proc_pidinfo`/`ps`.
3. Upgrade `DashboardView.axaml` with `AdaptiveGridPanel`, live metric charts, status pills, and skeleton placeholders.

### Phase 4: Server Settings (Full Modular Parity)
1. Split `ServerSettingsViewModel` and `ServerSettingsPage.axaml` into 9 modular tabs:
   - General (with `MinecraftMotdConverter` live preview and `ImageCropPage.axaml` SkiaSharp 64x64 icon cropper).
   - Addons (with enable/disable toggle, icon caching, and update check dialog).
   - World (with `WorldManager` multi-world switching and dimension cleanup).
   - Backups (with local retention and cloud sync).
   - Performance (JVM flags, memory sliders, GC presets).
   - Bedrock (BDS and PocketMine-MP options).
   - Version Updates (transactional version changer).
   - Summaries (per-server AI session summary history).
   - Advanced (raw properties, custom JVM args, RCON, reboot schedules).

### Phase 5: Instance Creation, Import/Export, Marketplace & Modpacks
1. Upgrade `NewInstancePage.axaml` with engine download progress and runtime provisioning.
2. Port `InstanceExportPage.axaml` and `InstanceImportPage.axaml` with the standardized cross-platform `InstanceExportManifest` format.
3. Port `PluginBrowserPage.axaml` and `MapBrowserPage.axaml`, with `AddonInstallDialogWindow.axaml` and `ModpackInstallDialogWindow.axaml`.

### Phase 6: Networking, Port Reliability Engine & Tunnel Orchestration
1. Port full Port Reliability Engine: `PortCheckRequest`, `PortCheckResult`, `PortConflictInfo`, `PortLease`, `PortProbeFailureClassifier`, `PortFailureMessageService`, `PortRecoveryRecommendation`, and `PortConflictWindow.axaml`.
2. Implement POSIX owner identification via `ss` and `lsof`.
3. Port `InstanceTunnelOrchestrator.cs` and `CreateTunnelDialog.axaml`, `PlayitConsoleWindow.axaml`, and `PlayitStatusPage.axaml`.

### Phase 7: Console, Players, AI Intelligence, News, About & Remote Control UI
1. Console: Port `ConsoleLogFilter`, `ConsoleLogHistoryService`, and `LogLineClassifier` to `ServerConsolePage.axaml`.
2. Players: Upgrade `PlayerManagementPage.axaml` with `PlayerViewModel`, gamemode parser, and ban sidecar.
3. AI / Intelligence: Port `OllamaModelManagerDialog.axaml`, `ServerCrashDialogWindow.axaml`, and `SessionSummarizationService`.
4. News & About: Port `NewsPage.axaml` and `AboutPage.axaml` with contributor avatars.
5. Remote Control UI: Port full modern UI in `RemoteControlPage.axaml`.

### Phase 8: POSIX Hardening, Packaging & Verification Gate
1. Process group management verification (`setpgid`/`SIGTERM`/`SIGKILL`).
2. Automatic `chmod +x` application for extracted binaries.
3. Strict case-sensitive filesystem path verification.
4. Packaging scripts: `install.sh`, `uninstall.sh`, `build-dmg.sh`, `shell.nix`.
5. Full test suite execution: unit tests, integration tests, and architecture tests.
