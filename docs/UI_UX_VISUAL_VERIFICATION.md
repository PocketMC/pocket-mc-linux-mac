# PocketMC UI/UX Visual & Behavioral Verification Report

This document records the visual alignment, design system tokens, and interactive behavior verifications performed to ensure PocketMC Linux & macOS precisely matches the PocketMC Windows reference implementation.

---

## 1. Design System & Theme Token Parity

### Colors & Brushes
- **Card Background (Dark):** `#1E1E24` mapped to `CardBackgroundFillColorDefaultBrush` and `CardBackgroundBrush`.
- **Card Background (Secondary):** `#252528` mapped to `CardBackgroundFillColorSecondaryBrush`.
- **Card Stroke:** `#2D2D30` mapped to `CardStrokeColorDefaultBrush` and `CardBorderBrush`.
- **Controls & Inputs:** `#2A2A30` mapped to `ControlFillColorSecondaryBrush` and `InputBackgroundBrush`.
- **Primary Text:** `#F4F4F7` mapped to `TextFillColorPrimaryBrush` and `TextPrimaryBrush`.
- **Secondary Text:** `#A1A1AA` mapped to `TextFillColorSecondaryBrush` and `TextSecondaryBrush`.
- **Tertiary Text:** `#71717A` mapped to `TextFillColorTertiaryBrush` and `TextMutedBrush`.
- **Accent System:** Registered dynamic keys `AccentBrush`, `AccentPrimaryBrush`, `AccentFillColorDefaultBrush`, and `AccentTextFillColorPrimaryBrush` updated on custom/system palette change.
- **Accessible AI Action Style:** `Button.ai-action` provides `#0066D6` background, `#2A85FF` border, and high-contrast `#FFFFFF` text matching Windows `AiActionButtonStyle`.

---

## 2. Shell & Window Chrome Verification

- **Window Sizing & Backdrop:** `MinWidth="1024"`, `MinHeight="680"`, `Width="1100"`, `Height="740"`. Skia-based blurred wallpaper layer and tint overlays replicate Windows backdrop aesthetics.
- **Top Update Banner:** Positioned at the top above the navigation rail. Displays indeterminate spinner, progress bar, status message, "Restart to update" primary button, and dismiss action.
- **Navigation Rail:** Sized to `240px` width.
  * Main Menu: Dashboard (Ctrl+1), Tunnel (Ctrl+2), Remote Control (Ctrl+3), News (Ctrl+7 with unread badge `#C42B1C`).
  * Footer Menu: Settings (Ctrl+5), Runtimes (Ctrl+4), About (Ctrl+6).
- **TitleBar Context:** Server name pill with live status dot and bullet separator (`Text="  •  "`) displayed on detail view navigation.
- **Host Health Pill:** Right-aligned activity badge displaying live host health status from `INativeMetricsProvider`.

---

## 3. Dashboard & Instance Cards Verification

- **Header:** Title "Instances" paired with "New Instance" primary action button.
- **Skeleton Loaders:** 4-card pulsing grid active during initial asynchronous registry load.
- **Empty State:** Centered card with `CornerRadius="16"`, circular package badge, "No instances found" header, subtitle, and "Create Instance" button.
- **Instance Cards:**
  * Header: Server Name, Pin icon, Minecraft version badge, Engine type badge, Cross-play badge, Simple Voice Chat badge, Description, and live Status indicator.
  * Context Menu: Pin/Unpin, Copy Crash Report, Open Folder, Export Instance, Delete Instance.
  * Metadata Grid: 3-column `UniformGrid` displaying `Last Played` (or `Uptime`), `Created`, and `Platform`.
  * Metrics Grid: 3-column `UniformGrid` displaying `CPU`, `RAM`, and `PLAYERS` button (navigates to player management).
  * Connections Block: Dynamic display of Primary Tunnel IP, LAN IP, and Bedrock Numeric / Hostname tunnels with one-click copy buttons.
  * Action Buttons (Stopped): 2x2 grid containing `Start` (Primary), `Logs` (Secondary), `Files` (Secondary), `Settings` (Secondary).
  * Action Buttons (Running): 2x2 grid containing `Stop` / `Abort` (Danger), `Restart` (Secondary), `Console` (Primary), `Settings` (Secondary).

---

## 4. New Instance Wizard Verification

- **Top Header:** Back button, "Create a New Instance" title, "Import Modpack" (with BETA badge), and "Import Instance" button.
- **Left Column (Identity & Software):**
  * Server Name and Description input fields.
  * Server Type ComboBox supporting Vanilla, Paper, Fabric, Forge, NeoForge, Bedrock (BDS), Pocketmine (PHP), and Pumpkin.
  * Minecraft Version ComboBox with asynchronous provider lookup and "Show Snapshots" toggle.
  * Cross-play CheckBox ("Enable Cross-play (Geyser + Floodgate)").
- **Right Column (World & Gameplay Settings):**
  * World Seed, World Generation Type (Default, Flat, LargeBiomes, Amplified).
  * Default Gamemode (Survival, Creative, Adventure, Spectator), World Difficulty (Peaceful, Easy, Normal, Hard).
  * Players Limit and Custom World Upload (.zip / .mcworld) with native file picker browse.
- **Sticky Footer Action Bar:**
  * EULA acceptance checkbox with hyperlink to Mojang EULA.
  * Asynchronous download progress bar, stage indicator, and status text.
  * "Cancel" and "Create and Download" buttons.

---

## 5. Server Console Verification

- **Action Toolbar:** Back button, "Auto-scroll" toggle, "Copy Logs" button, search input, regex search toggle (`.*`), log level filter toggles (`Chat`, `Info`, `Warn`, `Error`, `Sys`), and "AI Summary" high-contrast button.
- **Terminal Area:** Monospace typography (Cascadia Code, JetBrains Mono, Consolas, Menlo), ANSI color formatting, active terminal status pill, log entry counter, and automated crash diagnostics alert banner.
- **Command Input:** Live command text box with command history navigation (Up/Down arrow keys) and primary "Send" button.

---

## 6. Test Suite & Verification Results

```
Test run for D:\Projects\PocketMC\pocket-mc-linux-mac\PocketMC.Tests\bin\Release\net8.0\PocketMC.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   212, Skipped:     0, Total:   212, Duration: 34 s - PocketMC.Tests.dll (net8.0)
```

- Total Tests: 212
- Passing: 212
- Failing: 0
- Skipped: 0
