# Phase 2: Instance Lifecycle & Console - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-14
**Phase:** 2-Instance Lifecycle & Console
**Areas discussed:** Instance Storage Layout, Process Tree Cleanup, Console Log Stream, Player List Config

---

## Instance Storage Layout

| Option | Description | Selected |
|--------|-------------|----------|
| Unique Suffix | Slugified Name with Unique Suffix (e.g., 'my-server_f8d4a3') | |
| UUID Only | UUID Only (e.g., 'f8d4a3a1-1234...') | |
| Slugified Only | Slugified Name Only (e.g., 'my-server') | ✓ |
| Agent Decide | You decide: Let the agent design the directory naming strategy. | |

**User's choice:** Slugified Name Only (e.g., 'my-server')
**Notes:** Maximizes human-readability on disk. Downstream services must handle name updates and directory renames.

---

## Instance Name Collision Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Sequential Number | Append a sequential number (e.g., 'my-server-1') | ✓ |
| Reject | Reject with error: Block the creation/cloning operation | |
| Agent Decide | You decide: Let the agent handle name collision resolution. | |

**User's choice:** Append a sequential number (e.g., 'my-server-1')
**Notes:** Follows standard OS copy conventions to ensure a smooth user experience and prevent data loss.

---

## Zip Slip Protection

| Option | Description | Selected |
|--------|-------------|----------|
| Abort & Exception | Abort entire extraction and throw an exception | ✓ |
| Skip & Warning | Skip invalid entries and log a warning | |
| Agent Decide | You decide: Let the agent determine Zip Slip protection strategy. | |

**User's choice:** Abort entire extraction and throw an exception
**Notes:** Hard fails immediately, cleans up partial directories, and alerts the user to guarantee absolute safety against path traversal exploits.

---

## Metadata Storage Registry

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid | Hybrid (Local 'instance.json' + Centralized listing cache) | ✓ |
| Centralized | Centralized only: All metadata stored in the application config folder. | |
| Local Only | Local 'instance.json' only: Store metadata inside the instance folder only. | |
| Agent Decide | You decide: Let the agent design the metadata registry system. | |

**User's choice:** Hybrid (Local 'instance.json' + Centralized listing cache)
**Notes:** Keeps instances portable while loading the dashboard quickly.

---

## Process Tree Tracking

| Option | Description | Selected |
|--------|-------------|----------|
| Process Groups | POSIX Process Groups (setpgid/kill -pgid) | ✓ |
| Scan Process Tree | Scan process tree with ps/pgrep | |
| Agent Decide | You decide: Let the agent choose the tracking mechanism. | |

**User's choice:** POSIX Process Groups (setpgid/kill -pgid)
**Notes:** Launch in its own process group and signal the negative group ID to capture and clean up grandchildren process trees natively.

---

## Graceful Stop Timeout

| Option | Description | Selected |
|--------|-------------|----------|
| Configurable 15s | Configurable with a 15-second default | ✓ |
| Fixed 10s | Fixed 10 seconds | |
| No Escalation | No automatic escalation | |
| Agent Decide | You decide: Let the agent set the default timeout behaviour. | |

**User's choice:** Configurable with a 15-second default
**Notes:** Gives large modded servers enough time to save chunk files to disk, while allowing faster timeouts for lighter engines.

---

## Graceful Stop Method

| Option | Description | Selected |
|--------|-------------|----------|
| Standard Input | Write native stop commands to Standard Input (stdin) | ✓ |
| POSIX SIGINT | POSIX SIGINT (Ctrl+C) first | |
| RCON connection | RCON client connection | |
| Agent Decide | You decide: Let the agent choose graceful stop strategy. | |

**User's choice:** Write native stop commands to Standard Input (stdin)
**Notes:** Write 'save-all' and 'stop' directly to the stdin stream of the running process, which is universal and works out-of-the-box.

---

## Crash Handling Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Rate-Limited Auto-Restart | Transition to 'Crashed' with rate-limited auto-restart | ✓ |
| Transition & Block | Transition to 'Crashed' and block | |
| Agent Decide | You decide: Let the agent design crash handling state machine. | |

**User's choice:** Transition to 'Crashed' with rate-limited auto-restart
**Notes:** Mark state as 'Crashed', log exit code, and try restarting up to 3 times (rate-limited) before giving up.

---

## Console Log Buffering

| Option | Description | Selected |
|--------|-------------|----------|
| Ring Buffer | Thread-safe Ring Buffer (e.g., last 1000 lines) | ✓ |
| Full Session Cache | Full Session Cache | |
| Agent Decide | You decide: Let the agent design in-memory console log buffer. | |

**User's choice:** Thread-safe Ring Buffer (e.g., last 1000 lines)
**Notes:** Highly memory-efficient, prevents memory leaks on long-running servers, and provides enough back-history for the UI.

---

## Disk Logging Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated file | Stream stdout/stderr to a dedicated PocketMC file with rotation | ✓ |
| Tail Native Log | Tail the server's native log file | |
| Agent Decide | You decide: Let the agent design the log persistence on disk. | |

**User's choice:** Stream stdout/stderr to a dedicated PocketMC file with rotation
**Notes:** Capture early startup Java/PHP errors before the server engine loads. File named 'pocketmc-latest.log' under instance folder, rotated daily.

---

## ANSI Processing Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Parse UI / Plain Disk | Parse for UI styling, write plain text to disk | ✓ |
| Keep Raw ANSI | Keep raw ANSI codes everywhere | |
| Strip ANSI | Strip ANSI codes completely | |
| Agent Decide | You decide: Let the agent choose the ANSI parsing approach. | |

**User's choice:** Parse for UI styling, write plain text to disk
**Notes:** Keep files grep-friendly on disk while keeping the UI colorful.

---

## Log Search Implementation

| Option | Description | Selected |
|--------|-------------|----------|
| In-Memory search | In-Memory search over the buffered lines | ✓ |
| On-disk search | On-disk asynchronous search | |
| Agent Decide | You decide: Let the agent decide search and filtering logic. | |

**User's choice:** In-Memory search over the buffered lines
**Notes:** Quick, responsive, and low-overhead, searching only the current in-memory history.

---

## Player Config Sync Method

| Option | Description | Selected |
|--------|-------------|----------|
| Direct file + Watcher | Direct file edit + Filesystem watcher + Hot-reload commands | ✓ |
| Write files only | Write files only (No watcher) | |
| Stdin commands only | Send stdin commands only | |
| Agent Decide | You decide: Let the agent design the sync protocol. | |

**User's choice:** Direct file edit + Filesystem watcher + Hot-reload commands
**Notes:** Write directly to config files, watch for external updates, and run reload commands over stdin. Works whether online or offline.

---

## Player UUID Lookup Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Mojang API | Asynchronous Mojang API lookups with local caching | ✓ |
| Offline UUID | Offline UUID generation | |
| Agent Decide | You decide: Let the agent choose the UUID resolution policy. | |

**User's choice:** Asynchronous Mojang API lookups with local caching
**Notes:** Query Mojang API to resolve Java usernames to UUIDs dynamically and cache them. For Bedrock, parse local server files or offline generation.

---

## Player Tracking Method

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid | Hybrid (Log parsing + RCON verification) | ✓ |
| Log parsing only | Log parsing only | |
| Query/RCON poll only | Query/RCON polling only | |
| Agent Decide | You decide: Let the agent decide how to track active players. | |

**User's choice:** Hybrid (Log parsing + RCON verification)
**Notes:** Scan log streams for instant join/leave UI updates, and run a periodic RCON/query poll to keep the list verified.

---

## Player Config Engine Support

| Option | Description | Selected |
|--------|-------------|----------|
| Unified + Formatter | Unified player manager with engine-specific formatters | ✓ |
| Java-only support | Java-only support in Phase 2 | |
| Agent Decide | You decide: Let the agent design the cross-engine player adapter. | |

**User's choice:** Unified player manager with engine-specific formatters
**Notes:** Abstract player entries in UI, delegate formatting under the hood.

---

## the agent's Discretion

- JSON schema design for `instance.json` and centralized registry cache file.
- Thread-safe circular queue implementation details in C#.
- Regex patterns for player join/leave log parsing.
- Filesystem watcher and API client helpers structure.

## Deferred Ideas

None — all discussions stayed strictly within Phase 2 scope boundary.
