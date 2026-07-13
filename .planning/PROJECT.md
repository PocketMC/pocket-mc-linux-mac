# PocketMC

## What This Is

PocketMC is a native, cross-platform desktop application for Linux and macOS that enables users to manage Minecraft Java, Bedrock Dedicated Server, and PocketMine (PHP) servers. It provides a native desktop UI built on an async, service-oriented architecture designed to be API-first and easily extendable by AI.

## Core Value

Provide a reliable, native cross-platform manager for Minecraft Java, Bedrock Dedicated Server, and PocketMine servers with unified process monitoring and lifecycle management on Linux and macOS.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Core hosting environment using `Microsoft.Extensions.Hosting` and dependency injection
- [ ] Cross-platform secure storage (libsecret on Linux, Keychain on macOS, AES encrypted fallback)
- [ ] Unified storage directory layout in user home directories (~/.config or Library/Application Support)
- [ ] Server Instance lifecycle service (create, delete, rename, clone, import/export ZIP)
- [ ] Process tree tracking and child process cleanup (SIGTERM/SIGKILL runner replacing Windows Job Objects)
- [ ] Java runtime manager supporting Temurin API downloads (Java 8, 11, 17, 21, 25)
- [ ] PHP runtime manager supporting PocketMine automatic binary extraction and verification
- [ ] Server dashboard UI with live CPU, RAM, TPS, and player metrics
- [ ] Console terminal UI supporting colored logs, searching, filtering, and crash banners
- [ ] AI integration pipeline for log summarization and crash troubleshooting (OpenAI, Gemini, Claude, Ollama)
- [ ] Marketplace integration for Modrinth and CurseForge with dependency resolution
- [ ] Tunnel-based networking integration (Playit, Cloudflare Tunnel, Tailscale)
- [ ] Remote control Minimal API with QR pairing and web dashboard
- [ ] Backup system supporting ZIP archiving, checksums, scheduling, and cloud storage (Google Drive, Dropbox, OneDrive)
- [ ] Player list management (whitelist, ops, banlist, online player UUID lookups)
- [ ] Desktop UI visual polish (native dark/light themes, Sparkle/AppImage updates, sandboxed path safety)

### Out of Scope

- [Windows Job Objects] — Replaced entirely by Linux/macOS process tree tracking and signal management (SIGTERM/SIGKILL).
- [Velopack updates] — Replaced by Sparkle (macOS), AppImageUpdate (Linux), Flatpak/Snap, or custom GitHub releases updater.
- [Ad-hoc scripting] — All logic must compile into DI-resolved services in C# rather than shell script wrappers.

## Context

- The target platform is strictly Linux and macOS (removing legacy Windows assumptions/APIs).
- Current user preferences dictate a coarse granularity of phases and parallel execution where possible.
- Settings must be kept local to this machine.

## Constraints

- **Compatibility**: Must run natively on modern Linux distributions (via AppImage/Flatpak) and macOS (supporting Apple Silicon and Intel).
- **Security**: Secret keys, cloud credentials, and sensitive configurations must be stored securely using system keychains or AES fallback.
- **Resource Usage**: Server processes must be managed efficiently; orphaned processes must be cleaned up reliably upon server stop/crash.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Use Avalonia UI for frontend | Native C# look and feel, fits existing architecture better than Tauri or Flutter | — Pending |
| Use Microsoft.Extensions.Hosting for DI | Enables a clean service-oriented architecture where features are defined as clear services | — Pending |
| Cross-platform secure storage | Avoids DPAPI and relies on libsecret (Linux) and Keychain (macOS) | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-07-13 after initialization*
