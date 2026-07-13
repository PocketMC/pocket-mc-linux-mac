# Phase 1: Core Foundation & Provisioning - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-13
**Phase:** 1-Core Foundation & Provisioning
**Areas discussed:** Secure Storage Fallback Policy, Java & PHP Runtime Provisioning Mode, Custom Root Storage Overrides, Pre-Launch Binary Validation Action

---

## Secure Storage Fallback Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Silent AES Fallback | Encrypt credentials using AES with a machine-derived key silently, keeping the setup seamless. | ✓ |
| User-Provided Master Password | Prompt the user to establish a master password to encrypt local settings files, giving maximum control. | |
| Plaintext Fallback with Warning | Save secrets in plaintext and alert the user, maximizing ease of debugging. | |
| You decide | Let the agent select the appropriate fallback policy. | |

**User's choice:** Silent AES Fallback
**Notes:** Decided to keep the user experience seamless by generating a fallback AES key from machine metadata when platform keychains are unavailable.

---

## Java & PHP Runtime Provisioning Mode

| Option | Description | Selected |
|--------|-------------|----------|
| On-Demand Download | Only trigger a runtime installation when creating or starting a server instance that requires that version, minimizing startup delay and disk usage. | ✓ |
| Automatic Start Downloads | Detect missing runtimes on app startup and trigger background downloads automatically, ensuring the manager is immediately ready. | |
| Manual Manage UI | Provide a runtime management dashboard in settings for the user to explicitly install or remove runtimes. | |
| You decide | Let the agent select the provisioning mode. | |

**User's choice:** On-Demand Download
**Notes:** Download of Java or PHP runtime will trigger only when an instance is configured or run, avoiding downloading unneeded runtimes on clean startup.

---

## Custom Root Storage Overrides

| Option | Description | Selected |
|--------|-------------|----------|
| Configurable Root Path | Keep settings.json in the default OS folder, but allow configuring an alternate data path for instances/backups/downloads in settings.json. | ✓ |
| System-Default Location Only | Restrict all folders strictly to system defaults (~/.config/PocketMC or ~/Library/Application Support/PocketMC) for sandbox safety. | |
| Per-Instance Custom Path | Keep a global default root but allow specifying a completely custom directory for any individual instance during its creation. | |
| You decide | Let the agent select the storage layout customization policy. | |

**User's choice:** Configurable Root Path
**Notes:** Settings.json will remain in the standard user config path, but the root for instances, backups, cache, and logs can be customized.

---

## Pre-Launch Binary Validation Action

| Option | Description | Selected |
|--------|-------------|----------|
| Error Block & Repair UI | Block server launch, present a detailed error dialog with troubleshooting info, and offer to reinstall the runtime or change the path. | ✓ |
| Warning Bypass Dialog | Show a popup warning with the validation error details but allow the user to proceed with launch anyway. | |
| Silent Warning Log | Record the validation failure in logs but silently proceed with launching, avoiding any interruptions. | |
| You decide | Let the agent select the pre-launch validation action. | |

**User's choice:** Error Block & Repair UI
**Notes:** If validation fails, blocking the launch is safer to prevent corruption or unexpected state. A repair layout should guide the user.

## the agent's Discretion

- Choice of AES key derivation details (e.g., using a SHA-256 hash of the host machine ID).
- Exact format of settings.json schema for custom root paths and downloads metadata.

## Deferred Ideas

None.
