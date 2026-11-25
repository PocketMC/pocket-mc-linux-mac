# PocketMC Linux & macOS - Detailed Feature & UI/UX Parity Execution Plan

## 1. Post-v1.0.4.0-beta.1 Re-alignment

While `v1.0.4.0-beta.1` established core Avalonia navigation, basic instance management, Java 8-25 runtimes, cloud backup backends, Remote Control, and 231 passing automated tests, an exhaustive side-by-side audit against `pocket-mc-windows` (`feat/major-release-work`) identified visual token discrepancies (opaque surfaces blocking wallpaper translucency, theme dictionary accent precedence issues, missing shared styles) and simplified feature pages across Shell, Dashboard, New Instance, App Settings, Console, Players, Server Settings, Marketplace/Maps, Runtimes, Tunnels/Ports Map, Remote Control, and News/About.

This plan governs Phases A through H to achieve high-fidelity visual, structural, and functional parity with PocketMC Windows without breaking Clean Architecture or Linux/macOS native ergonomics.

---

## 2. Detailed Parity Phases (A - H)

### Phase A: Design System, Theme Tokens, Shell, Backdrop & `AppConfig`
- **A.1 `AppConfig` Single Source of Truth**: Port `PocketMC.Infrastructure/Configuration/AppConfig.cs` parsing `pocketmc.yml` (embedded/workspace config) dynamically for `version`, `channel`, `auth_proxies`, `telemetry_proxies`, and social/documentation URLs (`link_discord`, `link_github`, etc.). Wire `AppUrls` and `AboutViewModel.AppVersion` to `AppConfig`.
- **A.2 Translucent Theme System & `AccentColorService` Fix**:
  - Replace opaque `#1E1E24` / `#252528` / `#121214` fills in `App.axaml` Dark `ThemeDictionaries` with WPF-UI translucent white-alpha fills (`#0FFFFFFF`, `#15FFFFFF`, `#0BFFFFFF`) and subtle borders (`#18FFFFFF`) plus corresponding Light theme translucent surfaces.
  - Create shared `PocketMC.App/Styles/ControlStyles.axaml` (primary/secondary/danger/AI buttons, translucent `CardExpander` accordion style, `TextBox`, `ComboBox`, `ToggleSwitch`, scrollbars).
  - Fix `AccentColorService.ApplyAccentToApplication` to update both `ThemeDictionaries["Dark"]` and `ThemeDictionaries["Light"]` plus contrast-aware `TextOnAccentFillColorPrimaryBrush` and OS accent detection fallback on Linux/macOS.
- **A.3 Shell & Visual Assets**:
  - Fix `MainWindow.axaml` backdrop stack so `BackdropTintLayer` becomes transparent when FakeMica wallpaper blur is active.
  - Match Windows collapsible `NavigationView` rail toggle (48px compact vs 240px expanded), Windows-exact right-aligned `TitleBarContext` pill alongside System RAM, `SplashWindow` startup initialization & update check, and `AboutPage` (`Team & Contributors` CardExpander with circular avatars + 2-column Community / Support & Feedback cards).

### Phase B: Dashboard & Server Lifecycle Parity
- Port `ServerLifecycleService` and `InstancePortUpdateService`.
- Expand `InstanceCardViewModel` and `DashboardView.axaml` to match Windows: 4 skeleton loading cards, Online/Busy symbols + spinner, green active border, `Imported from Modpack` / VoiceChat / CrossPlay / Pumpkin badges, Uptime counter, all 4 connection skeleton rows + Bedrock LAN + Bedrock numeric + Geyser numeric rows, card keyboard navigation (`Enter`/`Space`), and Pin/Unpin dynamic section header.

### Phase C: New Instance Wizard, Pumpkin, Refreshable Providers & Import/Export
- Port `PumpkinProvider` and `PumpkinConfiguration` with Pumpkin TCP/UDP port panel and NXS Playit callout (`PumpkinPremiumCallout`).
- Add `IRefreshableServerSoftwareProvider` ("Refresh versions" button + status text), `LoaderVersionPanel` (Fabric/Forge/NeoForge loader picker), footer `ErrorCallout`, and download stage progress ring text in `NewInstancePage.axaml`.
- Port `ModpackInstallDialogWindow` entry flow from "Import Modpack" and complete `InstanceImportService` / `InstanceExportService` parity.

### Phase D: App Settings Full Accordion & Subsystems
- Rebuild `AppSettingsPage.axaml` and `AppSettingsViewModel.cs` with all 10 Windows `CardExpander` sections:
  1. App Behavior (start with OS, start/close to tray, telemetry, sleep prevention).
  2. Storage & Root Directory (move/open/change).
  3. Appearance (backdrop selector, accent presets + custom hex, blur/tint sliders, custom wallpaper).
  4. Notifications.
  5. Discord Rich Presence (all toggles + Live Profile Preview Active/Idle card).
  6. Cloud Backups (connect Google Drive, Dropbox, OneDrive via `CloudBackupSettingsViewModel` + OAuth UI).
  7. API Configuration (CurseForge key).
  8. AI Session Summarization (provider selector, key validation, model combo, endpoint URL, Ollama Local/Cloud mode, daemon status check, `OllamaModelManagerDialog` + `OllamaService`, auto-summarize toggles).
  9. Software Updates (check/channel/auto-check).
  10. Keyboard Shortcuts reference card.

### Phase E: Server Console, Session Summaries & Player Management
- Rebuild `ServerConsolePage.axaml` and `ServerConsoleViewModel.cs`: live `Players` / `Stop` / `Restart` toolbar buttons, `ReadOnlyLogInfoBar`, `ResourceWarningBar` (80% RAM + Optimize action), `CrashBanner` (`Copy Report` + `Dismiss` + inline log box), inline collapsible **AI Intelligence** drawer with `MarkdownRenderer`, and log line context menu (`AI Analyze Selection`).
- Port `SessionSummarizationService`, `SessionLogPreprocessor`, `SummaryEmojiFormatter`, and `SummaryStorageService`.
- Upgrade `PlayerManagementPage.axaml`, `PlayerManagementViewModel`, `PlayerDataService`, `BedrockPlayerDataService`, `WhitelistService`, and `ServerStateFileService`: sidebar sub-navigation (`Online Players`, `Ban List`, `Whitelist`), Op toggle, Gamemode ComboBox, progress rings, and inline Ban/Kick reason prompt.

### Phase F: Server Settings (All 9 Tabs), Addons, World, Backups & Cloud Sync
- Rebuild `ServerSettingsPage.axaml` across all 9 Windows tabs with sticky footer (`Auto-start server at PocketMC startup` + `Discard` + `Save Changes`):
  - General: clickable dirt-textured `dark_dirt.png` Server Card preview with icon hover-edit -> `ImageCropPage` (SkiaSharp 64x64), inline collapsible `MotdEditorMode` with section color palette (`§0`-`§f`), formatting buttons (`§l`, `§o`, `§n`, `§m`, `§k`, `§r`), and green signal bars (`0/20`).
  - Addons: Java `JavaModMetadataService` + Bedrock Behavior/Resource Packs (`BedrockAddonInstaller`, `AddonInventoryService`, `AddonToggleService`, `AddonUpdateCheckService`, `AddonAutoUpdateService`, `AddonIconService`, `AddonUpdateCheckDialogWindow`, `AddonUpdateDialogWindow`, `PendingAddonInstallService`).
  - World: `WorldManager` / `WorldInventory` multi-world switching, seed display, dimension reset.
  - Backups: KPI header (`TOTAL SIZE`, `LAST BACKUP`, `DISK SPACE`), `BedrockSnapshotService`, editable backup labels (`Add a label...`), delta size (`+0.0 MB`), and **Cloud Off-site Sync** per-provider toggles + remote backups list (`ServerCloudBackupViewModel`).
  - Performance, Bedrock, Version Updates, Restart & Recovery / Advanced raw properties, and AI Summaries tabs.

### Phase G: Marketplace, Modpacks, Maps & Runtimes
- Rebuild `PluginBrowserPage.axaml` (Mods/Plugins/Modpacks filters matching Windows) and full `MapBrowserPage.axaml`, plus `AddonIconService`, `ModpackService`, `AddonInstallDialogWindow` (version/loader picker), `DependencyConfirmationWindow`, and `ModpackInstallDialogWindow`.
- Upgrade `JavaManagementPage.axaml` and `JavaManagementViewModel.cs` to match Windows `JavaSetupPage`: Java + PocketMine PHP runtimes (`8.2`, `8.3`, `8.0`, `8.1`), `READY`/`MISSING` badges, disk folder size in MB, status glyphs, footer summary, and `RuntimeDownloadDialog`.

### Phase H: Tunnels, Ports Map, Remote Control & News
- Rebuild `TunnelPage.axaml` + `PlayitStatusPage` + `PreStartAgentWarningWindow` + `PlayitAgentService` / `PlayitApiClient` / `InstanceTunnelOrchestrator` parity (Network Status header, Agent Status card with `Special Thanks to Playit.gg` gradient, framed public URL + green Port + Save + tag + toggle/delete/copy).
- Polish `PortsMapPage.axaml` with expandable `CardExpander` headers, colored Canvas/SVG routing connectors, and status badges.
- Port `CloudflaredInstaller` (including macOS tarball extraction), `PlayitHttpsTunnelProvider` full parity, and `RemoteControlPage.axaml` accent tabs + Discord DM expander.
- Port `NewsPopupWindow` and `NewsContentRenderer` parity.

---

## 3. Quality & Verification Rules

- Write one failing test first, verify RED, implement minimal production-grade fix, verify GREEN.
- Build and run full test suite (`dotnet build PocketMC.sln -c Release` and `dotnet test PocketMC.sln -c Release`) after every phase.
- Update `docs/WINDOWS_PARITY_MATRIX.md`, `docs/UI_UX_PARITY_MATRIX.md`, and `.ai/STATE.md` honestly after each phase.
