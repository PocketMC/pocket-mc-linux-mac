# Phase 2: Instance Lifecycle & Console - Context

**Gathered:** 2026-07-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement the instance lifecycle service (create, delete, rename, clone, import/export ZIP), process runner with POSIX process group tree tracking and SIGKILL timeout, real-time colored console log streams (circular buffer + rotated disk logging), and player configurations synchronization (direct edit + watcher).
</domain>

<decisions>
## Implementation Decisions

### Instance Storage Layout
- **D-01:** Slugified Name Only: Instance folders are named using the slugified name of the instance (e.g. `my-server`).
- **D-02:** Collision Resolution: When creating or cloning a server instance with a name that already exists, append a sequential number (e.g. `my-server-1`).
- **D-03:** Zip Slip Protection: Abort the entire extraction, delete the partial directory, and notify the user to ensure absolute security if any path escapes the instance directory.
- **D-04:** Metadata Registry: Use a hybrid metadata registry where each instance folder contains its own local `instance.json` (for portability), and a centralized JSON cache is maintained in the root settings folder for fast dashboard loading.

### Process Tree Cleanup
- **D-05:** POSIX Process Groups: Launch the server process in its own group using `setpgid` and signal the negative process group ID (`kill -pgid`) to reliably capture and clean up all child/grandchild processes on Linux/macOS.
- **D-06:** SIGKILL Timeout: Graceful stop timeout is configurable with a default duration of 15 seconds before escalating to a forceful `SIGKILL`.
- **D-07:** Stop Commands: Execute graceful shutdown by writing native console commands (e.g., `save-all` and `stop`) directly to the stdin stream of the running process.
- **D-08:** Crash Handling: Transition instance state to `Crashed` upon non-zero exit codes, log the exit code, and attempt rate-limited auto-restart up to 3 times if enabled.

### Console Log Stream
- **D-09:** Log Buffer Size: Maintain a thread-safe circular/ring buffer in memory of the last 1000 lines of console output for the dashboard view to prevent memory leaks.
- **D-10:** Log Persistence: Actively capture stdout/stderr streams and persist them to a dedicated `pocketmc-latest.log` file in the instance folder, rotated daily.
- **D-11:** ANSI Formatting: Strip ANSI escape sequences when writing logs to disk, but parse and convert them to structured formatting tokens for the colored UI dashboard console.
- **D-12:** Console Search Logic: Perform fast in-memory search and filtering over the circular console line buffer.

### Player List Config
- **D-13:** Player Config Sync: Directly write edits to the instance configuration files (JSON/TXT), monitor them using a filesystem watcher for external updates (e.g. in-game commands), and run a reload command over stdin if the server is running.
- **D-14:** Player UUID Resolution: Query Mojang's public profile APIs asynchronously to resolve Java usernames to UUIDs with local caching; parse local permissions/files or use offline generation for Bedrock.
- **D-15:** Active Player Monitoring: Use log regex parsing for instant join/leave UI updates combined with periodic Query/RCON polling to verify the list of online players.
- **D-16:** Cross-Engine Player Config Adapters: UI uses an abstracted player entry model, and delegates serialization/deserialization to engine-specific adapters (JSON for Java/Bedrock, TXT for PocketMine).

### the agent's Discretion
- The precise JSON schemas for `instance.json` and the centralized registry/cache.
- The details of the thread-safe circular buffer implementation in C# (e.g. using a bounded ConcurrentQueue or array-based circular queue).
- The implementation details of the filesystem watcher, Mojang API client helper, and log parsing regex patterns.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Scope & Goals
- `.planning/PROJECT.md` — Project definition, core value, and constraints.
- `.planning/REQUIREMENTS.md` — Detailed requirements for INST, CONS, PLYR.
- `.planning/ROADMAP.md` — Success criteria and goal definition for Phase 2.

### Codebase Integration Points
- `PocketMC.App/Program.cs` — Service provider configurations and registrations.
- `PocketMC.Core/Services/IJavaService.cs` — Java runtime directory and execution verification helper interface.
- `PocketMC.Core/Services/IPHPService.cs` — PHP runtime directory and execution verification helper interface.
- `PocketMC.Core/Services/ISettingsService.cs` — Root configuration and instance path provider interface.
- `PocketMC.Core/Services/IPreLaunchVerifier.cs` — Runtime and path pre-launch verification interface.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ISettingsService` & `SettingsService`: Interface and implementation for managing path roots (config directory, instances root directory, settings files).
- `IPreLaunchVerifier` & `PreLaunchVerifier`: Can be extended/called by the process runner to verify that required Java/PHP paths are valid before attempting launch.

### Established Patterns
- DI Service Registrations: Services are registered as Singletons in the host builder within `PocketMC.App/Program.cs`. Interfaces belong in `PocketMC.Core/Services`, implementations in `PocketMC.Infrastructure/Services` (or `PocketMC.Platform/Services` for OS-specific logic).

### Integration Points
- Services registration: The new instance lifecycle and process tracking services must be added to the DI container in `PocketMC.App/Program.cs`.

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 2-Instance Lifecycle & Console*
*Context gathered: 2026-07-14*
