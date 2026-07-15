<!-- GSD:project-start source:PROJECT.md -->

## Project

**PocketMC**

PocketMC is a native, cross-platform desktop application for Linux and macOS that enables users to manage Minecraft Java, Bedrock Dedicated Server, and PocketMine (PHP) servers. It provides a native desktop UI built on an async, service-oriented architecture designed to be API-first and easily extendable by AI.

**Core Value:** Provide a reliable, native cross-platform manager for Minecraft Java, Bedrock Dedicated Server, and PocketMine servers with unified process monitoring and lifecycle management on Linux and macOS.

### Constraints

- **Compatibility**: Must run natively on modern Linux distributions (via AppImage/Flatpak) and macOS (supporting Apple Silicon and Intel).
- **Security**: Secret keys, cloud credentials, and sensitive configurations must be stored securely using system keychains or AES fallback.
- **Resource Usage**: Server processes must be managed efficiently; orphaned processes must be cleaned up reliably upon server stop/crash.

<!-- GSD:project-end -->

<!-- GSD:stack-start source:STACK.md -->

## Technology Stack

Technology stack not yet documented. Will populate after codebase mapping or first phase.
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.agent/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
