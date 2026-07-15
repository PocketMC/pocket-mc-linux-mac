## Shell & Navigation Frame

The root host is `MainWindow` (a `FluentWindow`) containing a `NavigationView` (`RootNavigation`) with a sidebar and a content frame. All pages render inside this frame.

**Navigation types:**
- **Shell Pages** — top-level sidebar items (Dashboard, Tunnel, Remote Control, Java, Settings)
- **Detail Pages** — pushed onto a `ControlledNavigationStack` when drilling into an instance (Console, Server Settings, Players, Marketplace) [1](#0-0) [2](#0-1) 

---

## Page Architecture

### 1. Dashboard (`DashboardPage`)

**Entry point** — the default shell page shown on launch.

**Layout:**
- `WrapPanel` grid of **Instance Cards** (responsive, flows by window width)
- **"New Instance"** button (navigates to `NewInstancePage`)
- **Import Instance / Import Modpack** buttons in header

**Instance Card elements (per server):**
- Server icon + name + engine badge (Vanilla, Paper, Fabric, Forge, NeoForge, BDS, PocketMine)
- **Feature badges:** Cross-play (Geyser), Voice Chat
- **State-driven controls:**
  - `Stopped` → Start button, Settings button
  - `Online/Starting` → Stop button, Abort button, resource metrics (CPU, RAM, Players)
  - `Installing/SettingUp/Starting/Stopping` → `BusySpinner` overlay
- **Tunnel address rows** with skeleton loading animation while resolving
- **"Last Played"** relative time (e.g., "5 min ago")
- **Card action buttons:** Open Console, Server Settings, Players, Marketplace

**Data flow:** `InstanceRegistry` → `DashboardInstanceListViewModel` → `ObservableCollection<InstanceCardViewModel>` → XAML bindings [3](#0-2) [4](#0-3) 

---

### 2. New Instance Page (`NewInstancePage`)

**Triggered by:** "New Instance" button on Dashboard.

**Layout:** Responsive two-column grid (stacks below 850px width).

**Left Panel — Basics:**
- Instance name text field
- Server Type combo (`ComboServerType`) — selects engine, triggers `LoadVersionsAsync()`
- Version combo (`ComboVersion`) — populated from provider API
- Cross-play toggle (`ToggleGeyser`)

**Right Panel — World Settings:**
- World import button (`BtnSelectWorld_Click`) → `OpenFileDialog` for ZIP
- Map Browser shortcut (`BtnImportModpack_Click`)

**Header actions:**
- Back button
- Import Modpack button
- Import Instance button

**Footer Action Bar:**
- EULA acceptance checkbox (`ChkAcceptEula`)
- Download progress bar + progress text (shown during creation)
- **Create** button → triggers `CreateInstanceAsync()`

**Creation pipeline:** Validate → Create directory → Download software → Provision Geyser/Floodgate (if cross-play) → Import world ZIP → Navigate to Dashboard [5](#0-4) [6](#0-5) 

---

### 3. Server Console Page (`ServerConsolePage`) — Detail Page

**Triggered by:** "Open Console" on an instance card.

**Title bar:** Injects server name + status ("● Online", "✖ Crashed") via `ITitleBarContextSource`.

**Layout:**
- **Auto-scroll toggle** (`BtnAutoScroll`)
- **Log output area** — `ItemsControl` with `VirtualizingStackPanel`, color-coded by `LogLineClassifier` (Chat, Error/Warn, System, Info)
- **Filter/Search bar** — filters visible log lines
- **Command input bar** — sends commands to server STDIN; Up/Down arrows cycle command history
- **AI Summary panel** (`AiPanelColumn`) — collapsible side panel, triggered by "AI Summary" button
- **CrashBanner** — shown when `ServerState == Crashed`; includes "Copy Crash Report" shortcut
- **ResourceWarningBar** — shown when RAM > 80% of allocated limit
- **ReadOnlyLogInfoBar** — shown when viewing a stopped server's historical log

**Log pipeline:** `ServerProcess.OnOutputLine` → `ConcurrentQueue` → `DispatcherTimer` (50ms flush) → `ObservableCollection<LogLine>` → UI [7](#0-6) [8](#0-7) 

---

### 4. Server Settings Page (`ServerSettingsPage`) — Detail Page

**Triggered by:** "Settings" on an instance card or from Console page.

**Architecture:** `ServerSettingsViewModel` is a composite container of tab-specific sub-ViewModels. Uses a **Draft/Commit** model — changes are staged and only persisted on `SaveCommand`.

**Tabs / Sections:**

| Tab | Sub-ViewModel | Key Controls |
|---|---|---|
| **General** | `SettingsGeneralVM` | Instance name, description, MOTD editor (with live Minecraft color preview), server icon crop (`ImageCropPage`) |
| **World** | `SettingsWorldVM` | Seed, difficulty, gamemode, world directory picker |
| **Performance** | `SettingsPerformanceVM` | Min/Max RAM sliders, JVM optimization flags |
| **Addons** | `SettingsAddonsVM` | Installed plugin/mod list, toggle states, update checks |
| **Version Updates** | `SettingsVersionUpdatesVM` | Staged version updates, changelogs, rollback |
| **Advanced** | `SettingsAdvancedVM` | Raw `server.properties` editor, custom JVM flags |
| **Backups** | `SettingsBackupsVM` | Manual backup trigger, backup history list, restore button, cloud replication config |
| **AI Summaries** | `SettingsSummariesVM` | Saved AI session summaries history |
| **Players** | (links to `PlayerManagementPage`) | Whitelist, ban list, online players |

**Save flow:** `SaveCommand` → `ServerConfigurationService.Save()` → writes `server.properties` + `.pocket-mc.json`; if server is running, `ServerRuntimeSettingApplier` pushes live changes (difficulty, whitelist) via STDIN. [9](#0-8) [10](#0-9) 

---

### 5. Player Management Page (`PlayerManagementPage`) — Detail Page

**Triggered by:** "Players" card button or from Server Settings.

**Layout:** Internal `NavigationView` sidebar with three sections:

- **Online Players** — live list of connected players; per-player actions: Kick, Ban, Op/Deop, Gamemode badge
- **Ban List** — list of banned players; Unban action
- **Whitelist** — add player by name (with Mojang UUID lookup for Java online-mode), toggle whitelist on/off, remove player

**Data sync:** Subscribes to `ServerProcess.OnOnlinePlayersUpdated` + regex detection on STDOUT + `ServerStateFileService` file watchers for `ops.json`, `whitelist.json`, `banned-players.json`. [11](#0-10) [12](#0-11) 

---

### 6. Plugin Browser Page (`PluginBrowserPage`) — Detail Page

**Triggered by:** "Marketplace" card button.

**Layout:**
- Search bar with debounced API calls (cancels in-flight requests on new input)
- Contextual placeholder text based on engine (e.g., "Search Fabric mods..." vs "Search Spigot plugins...")
- Infinite-scroll results list (pagination via `_currentOffset`)
- Per-result: icon, name, description, download count, Install button

**Providers:** Modrinth (Java mods/plugins), CurseForge (Java + Bedrock), Poggit (PocketMine `.phar`)

**Install flow:** Select addon → `DependencyResolverService.ResolveAsync()` → `DependencyConfirmationWindow` (checklist of required + optional deps) → Download → `MarketplaceArchiveInspector` security scan → `AddonManifestService.RegisterInstallAsync()` [13](#0-12) [14](#0-13) 

---

### 7. Map Browser Page (`MapBrowserPage`) — Detail Page

**Triggered by:** "Import Map" from New Instance page or Marketplace.

**Layout:** Similar to Plugin Browser but scoped to CurseForge Class ID `17` (Worlds).

**Install flow:** Download to temp → `OnMapDownloaded` event → `WorldManager.ImportWorldZipAsync()` → copies into instance world directory. [15](#0-14) 

---

### 8. Tunnel Page (`TunnelPage`) — Sidebar Shell Page

**Sidebar label:** Tunnel / Public Access

**State-driven UI:**

| Agent State | UI Shown |
|---|---|
| `Missing` | "Download Agent" button |
| `Downloading` | `DownloadProgressBar` + percentage |
| `AwaitingSetupCode` | "Setup Agent" button → opens `PlayitSetupWizardDialog` |
| `Connected` | Active tunnel list (`TunnelList`), "Disconnect" button |
| `ReauthRequired` | Re-authentication prompt |
| `Error` | Error message |

**Tunnel list items:** Public address, protocol (TCP/UDP), local port, "Copy" button.

**`CreateTunnelDialog`:** Allows manually creating a new tunnel for a specific local port.

**`PortsMapPage`:** Sub-page showing all port mappings and diagnostics. [16](#0-15) [17](#0-16) 

---

### 9. Remote Control Page (`RemoteControlPage`) — Sidebar Shell Page

**Sidebar label:** Remote Control

**Layout:**
- Enable/Disable toggle
- Port number field
- **Access Mode** selector: LAN Only, Cloudflare Quick Tunnel (`.trycloudflare.com`), Playit HTTPS Tunnel (Premium)
- **Password** field (encrypted via DPAPI before storage)
- **Local QR code** — for LAN access via mobile
- **Public QR code** — generated when tunnel URL is active
- **Discord notification** toggle + Discord User ID field (sends DM when tunnel URL changes)
- Status display: local address, public URL

**Save behavior:** Critical changes (port, enabled state) trigger `SaveAndRestart()` → `RemoteControlCoordinator.RestartAllAsync()`. [18](#0-17) [19](#0-18) 

---

### 10. Java Setup Page (`JavaSetupPage`) — Sidebar Shell Page

**Sidebar label:** Java

**Layout:**
- Runtime list (`RuntimeList`) — one row per Java version (8, 11, 17, 21, 25)
- Per-row status badge: `READY` (✓), `MISSING`, `DOWNLOADING` (with progress)
- **"Download Missing"** button — triggers `JavaProvisioningService` for all absent versions
- **"Add Custom"** button — allows specifying a custom `java.exe` path
- **Refresh** button

**Status icons:** Segoe Fluent Icons (`\uE73E` success, `\uEA39` error). [20](#0-19) [21](#0-20) 

---

### 11. App Settings Page (`AppSettingsPage`) — Sidebar Shell Page

**Sidebar label:** Settings

**Sections:**

- **Appearance** — Window backdrop (Mica, Acrylic, FakeMica/Wallpaper Blur), accent color picker (16 presets)
- **Behavior** — Start with Windows toggle (registry), Minimize to Tray on Close toggle, Keep Computer Awake while servers run toggle
- **Storage** — App root directory picker, External backup directory picker
- **API Keys** — CurseForge API key, AI provider keys (Gemini, OpenAI) — all encrypted via DPAPI
- **Cloud Backup** — Google Drive, OneDrive, Dropbox OAuth token management
- **Dependency Health** — Status indicators for Java, PHP, Playit agent; "Fix" shortcuts
- **UWP Loopback** — Button to enable AppContainer loopback exemption for Bedrock Edition
- **Diagnostics** — "Export Support Bundle" button (`DiagnosticReportingService`)
- **Telemetry** — Opt-out toggle
- **About / What's New** — App version, changelog viewer [22](#0-21) [23](#0-22) 

---

### Complete User-Flow Diagram

```mermaid
graph TD
    "App Launch" --> "StartupUpdateWindow"
    "StartupUpdateWindow" --> "Root Dir Check"
    "Root Dir Check" -- "Missing" --> "Root Directory Setup (locked nav)"
    "Root Dir Check" -- "OK" --> "Dashboard"

    "Dashboard" --> "New Instance Page"
    "Dashboard" --> "Instance Card"

    "Instance Card" --> "Console Page"
    "Instance Card" --> "Server Settings Page"
    "Instance Card" --> "Player Management Page"
    "Instance Card" --> "Plugin Browser Page"

    "Plugin Browser Page" --> "Dependency Confirmation Dialog"
    "Plugin Browser Page" --> "Map Browser Page"

    "Server Settings Page" --> "Image Crop Page"
    "Server Settings Page" --> "Player Management Page"
    "Server Settings Page" --> "Backup History & Restore"

    "New Instance Page" --> "Map Browser Page"
    "New Instance Page" -- "Create" --> "Dashboard"

    subgraph "Sidebar Shell Pages"
        "Tunnel Page"
        "Remote Control Page"
        "Java Setup Page"
        "App Settings Page"
    end

    "Tunnel Page" --> "Playit Setup Wizard Dialog"
    "Tunnel Page" --> "Create Tunnel Dialog"
    "Tunnel Page" --> "Ports Map Page"

    "Remote Control Page" --> "Password Setup"

    "Console Page" --> "AI Summary Panel"
```
