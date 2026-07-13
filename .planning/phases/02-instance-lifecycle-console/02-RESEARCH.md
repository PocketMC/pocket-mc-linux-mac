# Phase 2: Instance Lifecycle & Console - Research

**Date:** 2026-07-14
**Status:** Research Complete

This document provides the technical research, implementation details, and API patterns required for the Instance Lifecycle & Console phase.

---

## 1. POSIX Process Group Management in .NET

To track and terminate the entire process tree on Linux and macOS, we will use native POSIX Process Groups via P/Invoke.

### P/Invoke Definitions

```csharp
using System.Runtime.InteropServices;

public static class UnixNative
{
    private const string Libc = "libc";

    [DllImport(Libc, SetLastError = true)]
    public static extern int setpgid(int pid, int pgid);

    [DllImport(Libc, SetLastError = true)]
    public static extern int kill(int pid, int sig);

    // POSIX Signal Constants
    public const int SIGINT = 2;
    public const int SIGTERM = 15;
    public const int SIGKILL = 9;
}
```

### Process Spawning & Grouping Workflow

1. **Spawn Child:** Start the server process using `System.Diagnostics.Process`.
2. **Assign Group:** Immediately call `UnixNative.setpgid(process.Id, process.Id)`. This sets the process group ID (PGID) to the child's PID.
   * *Note:* On Unix systems, calling `setpgid` on a child process from the parent is safe if executed before the child performs `exec`. Even if the child performs `exec` first, we handle errors gracefully (e.g. ignoring `EACCES` if the child already set its own group).
3. **Signal Group:** To send a signal (e.g., `SIGTERM` or `SIGKILL`) to the entire process group, we call `UnixNative.kill(-pgid, signal)`. Passing the negative of the PGID targets the process group.

---

## 2. In-Memory Ring Buffer for Log Streams

To buffer console output efficiently without memory leaks, we use a thread-safe circular buffer wrapper around `ConcurrentQueue<T>`.

### Design Pattern

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;

public class LogRingBuffer
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly int _maxSize;
    private readonly object _lock = new();

    public LogRingBuffer(int maxSize = 1000)
    {
        _maxSize = maxSize;
    }

    public void Add(string line)
    {
        _queue.Enqueue(line);
        while (_queue.Count > _maxSize)
        {
            _queue.TryDequeue(out _);
        }
    }

    public IReadOnlyList<string> GetSnapshot()
    {
        return _queue.ToArray();
    }
}
```

---

## 3. ANSI Escape Code Parsing

Server console outputs (especially Minecraft Java and Paper) contain ANSI escape sequences for coloring (e.g., `\u001b[31;1m`).

### ANSI Strip Pattern (For Disk)

To write clean, searchable log files on disk, we strip ANSI sequences using a regular expression:

```csharp
using System.Text.RegularExpressions;

public static class AnsiHelper
{
    private static readonly Regex AnsiRegex = new(
        @"\x1B\[[0-9;]*[a-zA-Z]", 
        RegexOptions.Compiled
    );

    public static string StripAnsi(string input)
    {
        return AnsiRegex.Replace(input, string.Empty);
    }
}
```

### ANSI Parser Pattern (For UI Tokens)

For rendering colored console text, we parse sequences like `\x1B[31m` (Red) or `\x1B[0m` (Reset) and split the text into spans/tokens containing the raw text and its associated style/color.

---

## 4. Player UUID & Config Serialization

### Java Mojang API Lookup

To resolve Java Minecraft usernames to UUIDs:
- **Endpoint:** `https://api.mojang.com/users/profiles/minecraft/{username}`
- **Response Format:**
  ```json
  {
    "name": "Username",
    "id": "e0bfa5c276324b1790101b0728c3127a"
  }
  ```
- **UUID Formatting:** The Mojang API returns an unhyphenated hex string. Minecraft's `ops.json` and `whitelist.json` files require the hyphenated UUID format (e.g. `e0bfa5c2-7632-4b17-9010-1b0728c3127a`).

### Cross-Engine Player Files

1. **Minecraft Java:**
   - `ops.json`: List of JSON objects with `uuid`, `name`, `level`, `bypassesPlayerLimit`.
   - `whitelist.json`: List of JSON objects with `uuid`, `name`.
   - `banned-players.json`: List of JSON objects with `uuid`, `name`, `created`, `source`, `expires`, `reason`.
2. **Bedrock Dedicated Server (BDS):**
   - `permissions.json`: List of JSON objects with `xuid`, `permission` (member, operator, etc.).
   - `whitelist.json`: List of JSON objects with `xuid`, `name`, `ignoresPlayerLimit`.
3. **PocketMine-MP (PHP):**
   - `ops.txt`: Simple text list of usernames, one per line.
   - `white-list.txt`: Simple text list of usernames, one per line.

---

## 5. Validation Architecture

To ensure the instance lifecycle, process runner, and configuration watches function correctly, we will verify the services using:
1. Standard C# unit tests using `Xunit` testing simulated CLI outputs and lifecycle state transitions.
2. Integration verifiers launching lightweight mock processes to test POSIX group tracking and `SIGTERM`/`SIGKILL` cleanup.
