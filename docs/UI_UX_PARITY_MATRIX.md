# PocketMC Windows vs. Linux & macOS UI/UX Parity Matrix

This document provides a screen-by-screen, control-by-control audit comparing the authoritative Windows desktop UI (WPF / .NET 8) with the Linux & macOS desktop UI (Avalonia UI / .NET 8).

**Status Legend:**
- **Verified:** Implemented, visually aligned, and behavioral parity verified with automated or manual tests.
- **Implemented:** Implemented in code and XAML, matching Windows layout and behavior.
- **Partial:** Structurally present, but missing specific badges, actions, or visual states.
- **Missing:** Not present in the Linux/macOS codebase.

---

## 1. Application Shell & Window Chrome

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Window Backdrop & Tinting | `FluentWindow` + Skia Mica/Wallpaper tint | Skia blurred wallpaper fallback + layered tint border | Verified | WallpaperBackdropService renders high-quality blurred backdrops with dynamic overlay tinting matching Windows dark mode `#202020`. |
| Top Update Banner | `Border` above navigation with spinner, progress bar, restart button | Top banner in `MainWindow.axaml` with download progress, restart button, and dismiss action | Implemented | Displays staging progress, download percentage, and one-click restart update application. |
| Left Navigation Bar | `ui:NavigationView` (`OpenPaneLength="240"`) | Avalonia Navigation Rail (`240px` width) | Implemented | Main navigation items: Dashboard (Ctrl+1), Tunnel (Ctrl+2), Remote Control (Ctrl+3), News (Ctrl+7 with unread badge). Footer items: Settings (Ctrl+5), Runtimes (Ctrl+4), About (Ctrl+6). |
| TitleBar Context Pill | TitleBar Header with server name, status dot, and separator | TitleBar context pill in `MainWindow.axaml` | Implemented | Dynamically reveals active server name and live status color dot during detail page navigation. |
| Global Health Indicator | Header pill displaying host CPU/RAM status | Global health pill with Lucide activity icon and live metric updates | Implemented | Bound to `INativeMetricsProvider` POSIX metric outputs. |
| Navigation Breadcrumb | Implicit page title | Breadcrumb tracking (`PocketMC / <Page>`) with Back button | Implemented | Supports Esc and Alt+Left hotkey navigation back to Dashboard. |

---

## 2. Dashboard & Server Instance Cards

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Page Header | "Instances" Title + "New Instance" Primary button | Header with "Instances" Title + "New Instance" Primary button | Implemented | Matches Windows layout without redundant header buttons. |
| Skeleton Loading State | 4-card pulsing skeleton grid with `SkeletonElement` and `SkeletonCardBorder` | 4-card pulsing skeleton grid with `Border.skeleton-card` and `Border.skeleton-element` | Implemented | Shimmer and pulse animations during initial server registry load. |
| Empty State | Centered 80x80 circular icon with `Box24`, "No instances found", and "Create Instance" button | Centered circular badge, title, subtitle, and primary creation button | Implemented | Displays when `Instances.Count == 0` and `IsLoading == False`. |
| Card Header | Name (Bold), Pin icon, Version badge, Type badge, Crossplay badge, Voice Chat badge, Status indicator, 3-dots more menu | Name, Pin icon, Version badge, Type badge, Crossplay badge, Voice Chat badge, Status dot/spinner, 3-dots flyout menu | Implemented | Full badge parity including Geyser crossplay and Simple Voice Chat indicators. |
| Card More Options Menu | Pin/Unpin, Copy Crash Report, Open Folder, Export Instance, Delete Instance | Flyout menu with Pin/Unpin, Copy Crash Report, Open Folder, Export Instance, Delete Instance | Implemented | Context menu parity on every card. |
| Metadata Row | 3-column grid: `Last Played`, `Created`, `Platform` | 3-column grid: `Last Played`, `Created`, `Platform` | Implemented | Exact 3-column layout and typography styles matching Windows metadata block. |
| Metrics Block | 3-column grid: `CPU`, `RAM`, `PLAYERS` button | 3-column grid: `CPU`, `RAM`, `PLAYERS` button | Implemented | Clicking `PLAYERS` navigates directly to live player management. |
| Connection Addresses | Primary tunnel, LAN IP, Bedrock numeric / Geyser tunnels with copy buttons | Primary tunnel, LAN IP, Bedrock numeric / Geyser tunnels with copy buttons | Implemented | Visual indicators for Desktop, Bedrock Cube, Home LAN, and NumberSymbol. |
| Action Buttons (Stopped) | 2x2 grid: `Start` (Primary), `Logs` (Secondary), `Files` (Secondary), `Settings` (Secondary) | 2x2 grid: `Start`, `Console/Logs`, `Files`, `Settings` | Implemented | Exact 4-button layout when server is stopped. |
| Action Buttons (Running) | 2x2 grid: `Stop/Force Stop` (Danger), `Restart` (Secondary), `Console` (Primary), `Settings` (Secondary) | 2x2 grid: `Stop/Force Stop`, `Restart`, `Console`, `Settings` | Implemented | Exact 4-button layout when server is running. |

---

## 3. New Instance Wizard

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Top Header | Back button, "Create a New Instance" title, "Import Modpack" (BETA), "Import Instance" | Back button, "Create a New Instance" title, "Import Modpack", "Import Instance" | Implemented | Clean top header matching Windows. |
| Left Card: Identity & Software | Server Name, Description, Server Type combo, Minecraft Version combo, Refresh button, Pumpkin settings, Loader version, Geyser crossplay toggle | Server Name, Description, Server Type combo, Minecraft Version combo, Refresh button, Pumpkin settings, Loader version, Geyser crossplay toggle | Implemented | Supports Vanilla, Paper, Fabric, Forge, NeoForge, Bedrock (BDS), Pocketmine (PHP), and Pumpkin. |
| Right Card: World & Gameplay | World Seed, World Generation Type combo, Default Gamemode combo, World Difficulty combo, Players Limit, Custom World Upload with Browse | World Seed, World Generation Type combo, Default Gamemode combo, World Difficulty combo, Players Limit, Custom World Upload with Browse | Implemented | Supports custom ZIP and `.mcworld` imports. |
| Footer Action Bar | EULA acceptance checkbox with hyperlink, download progress bar/ring/stage text, Cancel and "Create and Download" buttons | EULA acceptance checkbox with hyperlink, download progress bar/ring/stage text, Cancel and "Create and Download" buttons | Implemented | Full transaction safety and progress monitoring. |

---

## 4. Server Console & Logs

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Action Toolbar | Back, Auto-scroll toggle, Copy Logs, Search input, Regex toggle, Chat/Info/Warn/Error/Sys filters, Players button, Stop button, Restart button, AI Summary button | Back, Auto-scroll toggle, Copy Logs, Search input, Regex toggle, Chat/Info/Warn/Error/Sys filters, Players button, Stop button, Restart button, AI Summary button | Implemented | Complete log inspection and process control toolbar. |
| Crash Banner | Critical Crash Detected alert with "Analyze with AI", "Copy Report", and Dismiss actions | Crash banner with crash log viewer, copy report, and AI analysis integration | Implemented | Pops up automatically on crash exit code. |
| Console Output Area | Monospace log view with colorized ANSI log formatting and right-click context menu | Monospace log view with ANSI color rendering and context menu | Implemented | Efficient buffering and scroll handling. |
| AI Intelligence Drawer | Collapsible side panel with Markdown analysis results | AI side panel with Markdown diagnostic rendering | Implemented | Integrated with Ollama / OpenAI / Anthropic providers. |
| Command Input Bar | Text input with history navigation (Up/Down) and "Send" button | Text input with history navigation and "Send" button | Implemented | Live command dispatch to running server process. |

---

## 5. Server Settings (All 9 Tabs)

| Tab | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Identity / General | Server name, description, motd with color preview, server icon crop dialog, auto-start toggle | Server name, description, motd with color preview, server icon crop dialog, auto-start toggle | Implemented | Full dynamic Minecraft motd color code parser and icon cropper. |
| Version Updates | Current vs latest version display, loader update, update planner, non-destructive staging, rollback recovery | Current vs latest version display, loader update, update planner, non-destructive staging, rollback recovery | Implemented | Supports Vanilla, Paper, Fabric, Forge, NeoForge, Bedrock, and Pocketmine. |
| Gameplay | Gamemode, Difficulty, Hardcore, PVP, Flight, Command blocks, Spawn monsters/animals/NPCs, Nether, End | Gamemode, Difficulty, Hardcore, PVP, Flight, Command blocks, Spawn monsters/animals/NPCs, Nether, End | Implemented | Direct two-way binding to `server.properties`. |
| World | Level name, Level seed, Level type, Generator settings, Max world size, Simulation distance, View distance | Level name, Level seed, Level type, Generator settings, Max world size, Simulation distance, View distance | Implemented | Direct two-way binding to `server.properties`. |
| Addons | Plugin, Mod, Modpack, and Resource Pack manager with enable/disable, uninstall, and search | Addon management with drag-and-drop ingestion, enable/disable, uninstall, and search | Implemented | Zip Slip safe extraction and client-mod filtering. |
| Backups | Local backup snapshots, create backup button, restore backup, export backup, auto-backup schedule | Local backup snapshots, create backup button, restore backup, export backup, auto-backup schedule | Implemented | Mutual exclusion locking with active process operations. |
| Restart & Recovery | Auto-restart on crash toggle, max restarts threshold, scheduled reboot maintenance (Daily / Interval) | Auto-restart on crash toggle, max restarts threshold, scheduled reboot maintenance (Daily / Interval) | Implemented | Background scheduler with countdown warnings. |
| Advanced / Raw | Port settings, query, RCON, memory allocation (Min/Max RAM), JVM arguments, raw property editor | Port settings, query, RCON, memory allocation (Min/Max RAM), JVM arguments, raw property editor | Implemented | Lossless server properties parser and validator. |
| AI Summaries | Session log diagnostics, historical summary list, prompt tuning | Session log diagnostics, historical summary list, prompt tuning | Implemented | Offline Ollama and cloud LLM execution. |

---

## 6. Marketplace & Addon Browsers

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Plugin & Mod Browser | Multi-source browser for Modrinth and CurseForge with search, category filters, and loader filter | Multi-source browser for Modrinth and CurseForge with search, category filters, and loader filter | Implemented | Full API integration with automatic jar resolution. |
| Dependency Dialog | Dependency confirmation dialog displaying required and optional dependencies | Dependency confirmation dialog with dependency hierarchy tree | Implemented | Safe resolution of mod requirements. |
| Modpack Installer | Manifest ingestion (`modrinth.index.json` / `manifest.json`), progress bar, override extractor | Manifest ingestion, progress bar, override extractor | Implemented | High-speed concurrent mod downloader. |

---

## 7. Networking, Tunnels & Ports Map

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Tunnel Management | Playit agent status, claim URL, active tunnels list, add tunnel dialog, reset agent | Playit agent status, claim URL, active tunnels list, add tunnel dialog, reset agent | Implemented | Complete tunnel provisioning and agent supervisor. |
| Playit Setup Wizard | Setup modal guiding agent download, installation, and account claiming | Setup modal guiding agent download, installation, and account claiming | Implemented | Automated agent provisioning for Linux and macOS. |
| Playit Console Window | Live terminal viewer for Playit agent logs with search and copy | Live terminal viewer for Playit agent logs with search and copy | Implemented | Real-time agent log streaming. |
| Ports Map Visualizer | Visual topology of active ports (Game, RCON, Query, Geyser, Voice Chat) with port editor | Visual topology of active ports with port editor and conflict detection | Implemented | Real-time port listening state checks. |
| Port Conflict Resolver | Modal dialog displaying conflicting process name, PID, and recommended alternative ports | Modal dialog displaying conflicting process name, PID, and recommended alternative ports | Implemented | Uses `ss` on Linux and `lsof` on macOS. |

---

## 8. Remote Control Dashboard & Web Host

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Desktop Configuration | Enable toggle, port, TLS certificate, LAN and Public QR codes, user accounts manager | Enable toggle, port, TLS certificate, LAN and Public QR codes, user accounts manager | Implemented | Full token and permission management. |
| Web Dashboard SPA | Mobile & Desktop web client with instances, console, players, properties, files, backups | Embedded SPA web client with complete REST API endpoints | Implemented | High-fidelity mobile and desktop web interface. |
| File Browser & Security | File tree, content editor, upload, download with symlink protection | File tree, content editor, upload, download with symlink protection | Implemented | Path traversal and token exposure mitigations. |

---

## 9. Personalization & App Settings

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| Appearance & Theming | Dark / Light theme selector, custom accent color palettes, wallpaper backdrop selector | Dark / Light theme selector, custom accent color palettes, wallpaper backdrop selector | Implemented | Real-time theme variant switching and accent synchronization. |
| System Behavior | Minimize to tray, start with system, sleep prevention toggle, hardware acceleration | Minimize to tray, start with system, sleep prevention toggle, hardware acceleration | Implemented | Cross-platform power assertions (`systemd-inhibit` / `caffeinate`). |
| Storage & Runtimes | Default storage root path selector, Java and PHP runtime manager | Default storage root path selector, Java and PHP runtime manager | Implemented | Safe storage root relocation with directory validation. |
| Settings Backup | Export and import application settings (.json) | Export and import application settings (.json) | Implemented | Full settings preservation and restore. |

---

## 10. Modals, Dialogs & Onboarding

| Screen / Component | Windows WPF Reference | Avalonia Target | Status | Audit Findings & Parity Details |
| :--- | :--- | :--- | :--- | :--- |
| First-Run Setup | Storage root directory selection dialog with validation and browse | Storage root directory selection dialog with validation and browse | Verified | Clean error handling and single-constructor DI activation. |
| What's New Window | Structured release notes dialog displaying new features, improvements, and fixes | Structured release notes dialog displaying new features, improvements, and fixes | Implemented | Automatically surfaces on new version launch. |
| Crash Analyzer Dialog | AI-powered crash log diagnostic dialog | AI-powered crash log diagnostic dialog | Implemented | Model selection and automated solution recommendations. |
| About Page | Studio attribution, Co-Founders, Contributors, Helpers, Donators, links, and license | Studio attribution, Co-Founders, Contributors, Helpers, Donators, links, and license | Implemented | Full attribution parity with DS Labs. |

---

## 11. News & Community Announcements

| Screen / Feature | Windows Specification (`PocketMC.Desktop`) | Linux/macOS Implementation (`PocketMC.App`) | Parity Status | Evidence & Verification Notes |
| :--- | :--- | :--- | :--- | :--- |
| News Page Layout | 2-column layout (300px list + article reader surface) | `NewsPage.axaml` 2-column grid (`300, 18, *`) | Verified | Dedicated announcements reader with list selection and empty state. |
| News Item Row | Title, unread indicator dot, type, priority, and date badge pills | `DataTemplate` with unread dot (`AccentFillColorDefaultBrush`) and 3 badge pills | Verified | Shows Type, Priority, and Published UTC date per item. |
| Article Reader | Card surface with badge pills, title, divider, and blocks | `Border` with `CardBackgroundFillColorDefaultBrush`, `ItemsControl` blocks | Verified | Renders formatted announcement content with automated mark-as-read. |
| News Service & Sync | GitHub content API sync with 6h interval and raw fallback | `NewsService.cs` with cache, state tracking, and GitHub repo content fetch | Verified | Caches news in `~/.pocketmc/news/cache/` with atomic write safety. |
| Keyboard Shortcut | `Ctrl+7` navigates to News | `MainWindow.axaml.cs` handles `Ctrl+7` to trigger `NavigateNewsCommand` | Verified | Full keyboard shortcut parity. |
