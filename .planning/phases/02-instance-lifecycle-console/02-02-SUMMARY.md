---
phase: 02-instance-lifecycle-console
plan: 02-02
subsystem: core
tags: [process, signals, console, logs, players, tracking]
requires:
  - phase: 02-instance-lifecycle-console
    provides: "IInstanceService"
provides:
  - "IProcessRunner interface"
  - "ProcessRunner implementation with setpgid process tree signals cleanup, shutdown timeout escalation, and auto-restart crashed loops"
  - "IConsoleLogService interface"
  - "ConsoleLogService implementation with circular ring buffer logging and daily rotating clean text writer"
  - "IPlayerService interface"
  - "PlayerService implementation supporting JSON / TXT configuration sync and log tracking player join/leaves"
  - "ProcessRunnerTests, ConsoleLogTests, and PlayerListTests suites"
affects:
  - 03-01
tech-stack:
  added: []
  patterns: POSIX Process Grouping, Circular Buffering, Watcher Filesystem Syncing
key-files:
  created:
    - PocketMC.Core/Services/IProcessRunner.cs
    - PocketMC.Core/Services/IConsoleLogService.cs
    - PocketMC.Core/Services/IPlayerService.cs
    - PocketMC.Infrastructure/Services/ProcessRunner.cs
    - PocketMC.Infrastructure/Services/ConsoleLogService.cs
    - PocketMC.Infrastructure/Services/PlayerService.cs
    - PocketMC.Tests/ProcessRunnerTests.cs
    - PocketMC.Tests/ConsoleLogTests.cs
    - PocketMC.Tests/PlayerListTests.cs
  modified:
    - PocketMC.App/Program.cs
key-decisions:
  - "Process tree tracking uses POSIX process groups (PGID) and signals sent to negative PGID."
  - "Graceful shutdown timeout defaults to 15 seconds before escalating to SIGKILL."
  - "Crashed state with rate-limited auto-restart loops."
  - "Strip ANSI formatting codes for logs saved to disk while keeping them for UI tokens."
patterns-established:
  - "POSIX Process Group Signals wrapper for .NET Process."
  - "Thread-safe ConcurrentQueue circular logging buffer."
requirements-completed:
  - INST-03
  - INST-04
  - CONS-01
  - CONS-02
  - PLYR-01
  - PLYR-02
duration: 25min
completed: 2026-07-14
---

# Phase 2 Plan 02: Execution & Monitoring Summary

**POSIX Process Group runner, terminal console buffering and log search engine, and player list configurations management.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-14T00:45:00Z
- **Completed:** 2026-07-14T01:10:00Z
- **Tasks:** 5
- **Files modified:** 1

## Accomplishments
- Implemented `IProcessRunner` and Unix-native P/Invoke `setpgid` and `kill` logic for process tree control.
- Designed process-exit crash handlers with rate-limited auto-restarts (up to 3 times in 5 minutes).
- Built `ConsoleLogService` storing up to 1000 lines in memory with daily text rotation to `pocketmc-latest.log` stripped of ANSI codes.
- Added `PlayerService` supporting Java JSON files, Bedrock JSON files, PocketMine TXT files, and Mojang username profile resolver with local offline MD5 generation fallback.
- Registered all interfaces in `Program.cs`.
- Developed unit test suites confirming process runner PGID signals escalation, log search, ANSI clean-ups, player synchronization configs, and regex log parser tracking.

## Task Commits

Each task was committed atomically:

1. **Task 1: Define DI Interfaces** - `11ac6cf` (feat(02-02): implement process runner with process grouping, log streaming, and player management)
2. **Task 2: Build POSIX Process Group Runner** - `11ac6cf` (feat(02-02): implement process runner with process grouping, log streaming, and player management)
3. **Task 3: Build Console Log Stream Engine** - `11ac6cf` (feat(02-02): implement process runner with process grouping, log streaming, and player management)
4. **Task 4: Build Player list Management Service** - `11ac6cf` (feat(02-02): implement process runner with process grouping, log streaming, and player management)
5. **Task 5: Implement unit and integration tests** - `11ac6cf` (feat(02-02): implement process runner with process grouping, log streaming, and player management)

## Files Created/Modified
- `PocketMC.Core/Services/IProcessRunner.cs` - IProcessRunner interface
- `PocketMC.Core/Services/IConsoleLogService.cs` - IConsoleLogService interface
- `PocketMC.Core/Services/IPlayerService.cs` - IPlayerService interface
- `PocketMC.Infrastructure/Services/ProcessRunner.cs` - Unix-native PGID process runner
- `PocketMC.Infrastructure/Services/ConsoleLogService.cs` - Log buffer and rotator
- `PocketMC.Infrastructure/Services/PlayerService.cs` - Player list management
- `PocketMC.App/Program.cs` - DI setups
- `PocketMC.Tests/ProcessRunnerTests.cs` - Runner SIGKILL and restart loop tests
- `PocketMC.Tests/ConsoleLogTests.cs` - Buffer and regex strip tests
- `PocketMC.Tests/PlayerListTests.cs` - JSON/TXT parser and resolver tests

## Decisions Made
- Configured a shutdown timeout property on `ProcessRunner` that defaults to 15 seconds to allow servers to safely write blocks before raising signals.
- Strip ANSI formatting sequences from disk writes to conserve space and ensure readable clean files.

## Deviations from Plan
- Created `UnixNative` P/Invoke mapping libc directly.

## Issues Encountered
- Crashing mock shell commands required careful argument escaping on `/bin/sh` or matching OS utilities. Fixed by using Unix-standard `/bin/false` utility ensuring exit code 1.

## Next Phase Readiness
- Ready for Phase 3 UI development with Avalonia (03-01).
