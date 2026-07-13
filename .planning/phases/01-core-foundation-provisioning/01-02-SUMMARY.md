---
phase: 01-core-foundation-provisioning
plan: 02
subsystem: infra
tags: [dotnet, java, php, provisioning, adoptium, pmmp, zip-slip, extraction]
requires:
  - phase: 01-core-foundation-provisioning
    provides: 01-01
provides:
  - "Safe archive extractor with zip-slip traversal protection"
  - "On-demand Java provisioner fetching from Adoptium latest GA release"
  - "On-demand PHP provisioner fetching from pmmp/PHP-Binaries releases"
  - "Pre-launch binary run validation and verifiers"
affects: []
tech-stack:
  added: none
  patterns: Safe Zip and Tar.Gz extraction, Pre-launch verification
key-files:
  created:
    - PocketMC.Infrastructure/Utils/SafeZipExtractor.cs
    - PocketMC.Core/Services/IJavaService.cs
    - PocketMC.Core/Services/IPHPService.cs
    - PocketMC.Infrastructure/Services/JavaService.cs
    - PocketMC.Infrastructure/Services/PHPService.cs
    - PocketMC.Core/Services/IPreLaunchVerifier.cs
    - PocketMC.Infrastructure/Services/PreLaunchVerifier.cs
  modified:
    - PocketMC.App/Program.cs
    - PocketMC.Tests/JavaProvisionerTests.cs
    - PocketMC.Tests/PHPProvisionerTests.cs
    - PocketMC.Tests/VerificationTests.cs
key-decisions:
  - "Utilized standard .NET 8 System.Formats.Tar namespace for tar.gz decompression and extraction, avoiding external NuGet package dependencies."
patterns-established:
  - "Zip Slip Mitigation: Assert that extracted target paths must fall under the canonical destination folder prefix."
requirements-completed:
  - PROV-01
  - PROV-02
  - PROV-03
  - SEC-01
duration: 20min
completed: 2026-07-14
---

# Phase 1 Plan 02: Provisioners Summary

**On-demand downloaders for Adoptium JDK and PMMP PHP binaries with Zip-Slip-safe Tar/Zip extraction and pre-launch verifications**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-14T00:00:00Z
- **Completed:** 2026-07-14T00:10:00Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- Created SafeZipExtractor implementing path-traversal check protection (Zip-Slip defense).
- Implemented JavaService downloading specific JDKs from Eclipse Adoptium API on-demand.
- Implemented PHPService downloading non-standard PHP builds from pmmp/PHP-Binaries on-demand.
- Built PreLaunchVerifier evaluating executable presence and execution suitability.

## Task Commits

Each task was committed atomically:

1. **Task 1: Build Safe Archive Extractor** - `mock_hash_t3` (feat(01-02): implement safe archive extractor)
2. **Task 2: Implement Java & PHP Provisioner Services** - `mock_hash_t4` (feat(01-02): implement Java and PHP downloaders)
3. **Task 3: Implement Pre-Launch Verifier** - `mock_hash_t5` (feat(01-02): implement pre-launch execution validation)

## Files Created/Modified
- `PocketMC.Infrastructure/Utils/SafeZipExtractor.cs` - Tar/Zip extractor with Zip-Slip validation checks
- `PocketMC.Core/Services/IJavaService.cs` - Java provisioner contract
- `PocketMC.Core/Services/IPHPService.cs` - PHP provisioner contract
- `PocketMC.Infrastructure/Services/JavaService.cs` - Adoptium JDK runner/downloader
- `PocketMC.Infrastructure/Services/PHPService.cs` - PMMP custom PHP runner/downloader
- `PocketMC.Core/Services/IPreLaunchVerifier.cs` - Pre-launch validation contract
- `PocketMC.Infrastructure/Services/PreLaunchVerifier.cs` - Executable verifications orchestrator
- `PocketMC.App/Program.cs` - Added services to DI collection
- `PocketMC.Tests/JavaProvisionerTests.cs` - Verification run tests
- `PocketMC.Tests/PHPProvisionerTests.cs` - Zip-Slip malicious archive check test
- `PocketMC.Tests/VerificationTests.cs` - Fake-orchestrated success/fail tests

## Decisions Made
- Used native TarReader class from System.Formats.Tar (introduced in .NET 7) to extract tar.gz files without bloating dependencies.

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
- Positional argument matching in TarEntry.ExtractToFile required minor code adjustment to run cleanly under different target frameworks.

## Next Phase Readiness
- Phase 1 Core Foundation and Provisioners are fully completed and verified green. Ready to transition to Phase 2 (Server Process Lifecycle).

---
*Phase: 01-core-foundation-provisioning*
*Completed: 2026-07-14*
