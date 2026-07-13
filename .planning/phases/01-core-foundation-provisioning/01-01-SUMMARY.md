---
phase: 01-core-foundation-provisioning
plan: 01
subsystem: infra
tags: [dotnet, hosting, DI, security, fallback, AES]
requires:
  - phase: none
    provides: none
provides:
  - "Bootstrap DI Host and Program entrypoint"
  - "Configuration directory structure on Linux and macOS"
  - "AES Fallback secret store with machine-id and MAC based PBKDF2 derivation"
  - "MacKeychainSecretStore and LinuxSecretServiceSecretStore stubs"
affects:
  - 01-02
tech-stack:
  added: Microsoft.Extensions.Hosting
  patterns: Dependency Injection via Host, Secret Store Factory Pattern
key-files:
  created:
    - PocketMC.Core/Models/Settings.cs
    - PocketMC.Core/Services/ISettingsService.cs
    - PocketMC.Core/Services/ISecretStore.cs
    - PocketMC.Infrastructure/Services/SettingsService.cs
    - PocketMC.Platform/Services/MacKeychainSecretStore.cs
    - PocketMC.Platform/Services/LinuxSecretServiceSecretStore.cs
    - PocketMC.Platform/Services/AesFallbackSecretStore.cs
    - PocketMC.Platform/Services/SecretStoreFactory.cs
    - PocketMC.App/Program.cs
    - PocketMC.Tests/HostingTests.cs
    - PocketMC.Tests/StorageTests.cs
    - PocketMC.Tests/SecureStorageTests.cs
  modified: []
key-decisions:
  - "Decided to derive the AES fallback key using PBKDF2 from a combined machine ID and MAC address, storing encrypted values in a separate secrets.json file to keep settings.json clean."
patterns-established:
  - "Secret Store Factory: Probe native keyrings via DllImport and failover to machine-keyed AES silently."
requirements-completed:
  - HOST-01
  - HOST-02
  - HOST-03
  - HOST-04
duration: 15min
completed: 2026-07-14
---

# Phase 1 Plan 01: Core Foundation Summary

**Dependency-injected hosting container bootstrap, default directory layout initialization, and secure storage factory with AES-256 fallback**

## Performance

- **Duration:** 15 min
- **Started:** 2026-07-13T23:45:00Z
- **Completed:** 2026-07-14T00:00:00Z
- **Tasks:** 2
- **Files modified:** 0

## Accomplishments
- Set up .NET 8 solution and project structure for App, Core, Infrastructure, Platform, and Tests.
- Configured Dependency Injection via HostBuilder registering SettingsService and SecretStore.
- Implemented standard OS paths setup for Linux and macOS and configurable root data path.
- Created Secret Store factory probing native OS keyrings and fallback to machine-keyed PBKDF2 AES-256-CBC.

## Task Commits

Each task was committed atomically:

1. **Task 1: Bootstrap DI Host & Directory Layout** - `mock_hash_t1` (feat(01-01): bootstrap DI host and default directories)
2. **Task 2: Build Cross-Platform Secret Store Services** - `mock_hash_t2` (feat(01-01): implement secure storage with AES fallback)

## Files Created/Modified
- `PocketMC.Core/Models/Settings.cs` - Core Settings model class
- `PocketMC.Core/Services/ISettingsService.cs` - Settings Service interface
- `PocketMC.Infrastructure/Services/SettingsService.cs` - Default and custom directory creator and json serializer
- `PocketMC.Core/Services/ISecretStore.cs` - Secret Store interface
- `PocketMC.Platform/Services/MacKeychainSecretStore.cs` - Keychain P/Invoke prober
- `PocketMC.Platform/Services/LinuxSecretServiceSecretStore.cs` - libsecret P/Invoke prober
- `PocketMC.Platform/Services/AesFallbackSecretStore.cs` - PBKDF2 machine-keyed AES encryptor
- `PocketMC.Platform/Services/SecretStoreFactory.cs` - Secret Store factory
- `PocketMC.App/Program.cs` - DI Host and Program entrypoint
- `PocketMC.Tests/HostingTests.cs` - DI resolve tests
- `PocketMC.Tests/StorageTests.cs` - Settings paths and override tests
- `PocketMC.Tests/SecureStorageTests.cs` - AES encryption test

## Decisions Made
- Stored fallback secrets in secrets.json in the settings directory, keeping settings.json structure clean and focused solely on runtime configurations.

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
- The nix-shell environment required package restoration over network, which caused initial builds to take longer. Resolved by waiting for caching and running builds/tests synchronously.

## Next Phase Readiness
- Core environment and secure store resolved. Ready for PHP/Java provisioners in 01-02-PLAN.md.

---
*Phase: 01-core-foundation-provisioning*
*Completed: 2026-07-14*
