---
phase: 02-instance-lifecycle-console
plan: 02-01
subsystem: core
tags: [instance, crud, zipslip, safety, serialization]
requires:
  - phase: 01-core-foundation-provisioning
    provides: "ISettingsService"
provides:
  - "ServerInstance model representation"
  - "IInstanceService interface"
  - "InstanceService implementation with slugification, collision resolver, Zip Slip protection, import/export, and local-cached metadata registry"
  - "InstanceServiceTests verifying CRUD and safety"
affects:
  - 02-02
tech-stack:
  added: []
  patterns: Slugification, Zip Slip validation, local metadata caching
key-files:
  created:
    - PocketMC.Core/Models/ServerInstance.cs
    - PocketMC.Core/Services/IInstanceService.cs
    - PocketMC.Infrastructure/Services/InstanceService.cs
    - PocketMC.Tests/InstanceServiceTests.cs
  modified:
    - PocketMC.App/Program.cs
    - PocketMC.Infrastructure/Services/SettingsService.cs
key-decisions:
  - "Rename a server requires folder renaming on disk."
  - "Resolve name collisions by appending a sequential number suffix."
  - "Abort extraction and cleanup directories immediately if Zip Slip is detected."
patterns-established:
  - "Hybrid metadata registry caching: Central JSON cache/index file + local metadata files."
requirements-completed:
  - INST-01
  - INST-02
  - SEC-01
duration: 15min
completed: 2026-07-14
---

# Phase 2 Plan 01: Instance Lifecycle CRUD Summary

**Instance CRUD management service implementation with Zip Slip protection, metadata manifests, and configuration file parsers.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-07-14T00:30:00Z
- **Completed:** 2026-07-14T00:45:00Z
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments
- Defined `ServerInstance` model representing metadata (Name, Slug, Path, EngineType/Version, JvmArgs, etc.).
- Defined `IInstanceService` interface supporting CRUD, cloning, import/export, and lists.
- Implemented `InstanceService` with:
  - Name slugification and sequential renaming collision resolver.
  - Zip Slip path traversal mitigation: canonical path validation aborts extraction and cleans directories.
  - Hybrid caching registry synchronization.
- Wrote `InstanceServiceTests` validating slug generation, CRUD, rename/clone operations, and Zip Slip safety.

## Task Commits

Each task was committed atomically:

1. **Task 1: Define ServerInstance Model & IInstanceService Interface** - `eac7b10` (feat(02-01): implement ServerInstance and IInstanceService CRUD with Zip Slip safety)
2. **Task 2: Implement Instance Lifecycle Service** - `eac7b10` (feat(02-01): implement ServerInstance and IInstanceService CRUD with Zip Slip safety)
3. **Task 3: Add Unit & Integration Tests for Instance Lifecycle** - `eac7b10` (feat(02-01): implement ServerInstance and IInstanceService CRUD with Zip Slip safety)

## Files Created/Modified
- `PocketMC.Core/Models/ServerInstance.cs` - ServerInstance metadata and EngineType enum
- `PocketMC.Core/Services/IInstanceService.cs` - Interface for instance lifecycle CRUD
- `PocketMC.Infrastructure/Services/InstanceService.cs` - Implementation of instance lifecycle
- `PocketMC.Infrastructure/Services/SettingsService.cs` - Optional configuration directory override parameter in constructor for isolated testing
- `PocketMC.App/Program.cs` - Registered InstanceService in DI
- `PocketMC.Tests/InstanceServiceTests.cs` - Isolated unit tests verifying instance lifecycle features

## Decisions Made
- Confirmed that renaming an instance triggers a folder move/rename on disk to maintain consistency with the slug structure.
- Reconstructed the centralized caching registry on start if it is missing or corrupted, leveraging local instance.json files.

## Deviations from Plan
None.

## Issues Encountered
None.

## Next Phase Readiness
- Ready for Wave 2 execution tasks (ProcessRunner, ConsoleLogService, PlayerService).
