---
phase: 2
slug: instance-lifecycle-console
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-14
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | .NET xUnit |
| **Config file** | PocketMC.Tests/PocketMC.Tests.csproj |
| **Quick run command** | `dotnet test --filter "Category=Unit"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | INST-01 | T-02-01 | Path traversal protection | unit | `dotnet test --filter "FullyQualifiedName~InstanceServiceTests.ZipSlip"` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | INST-02 | — | Correct model type representation | unit | `dotnet test --filter "FullyQualifiedName~InstanceServiceTests.CreateInstance"` | ❌ W0 | ⬜ pending |
| 02-02-01 | 02 | 2 | INST-04 | T-02-02 | Process group isolation (setpgid) | unit | `dotnet test --filter "FullyQualifiedName~ProcessRunnerTests.ProcessGroup"` | ❌ W0 | ⬜ pending |
| 02-02-02 | 02 | 2 | CONS-01 | — | Buffering & output streaming | unit | `dotnet test --filter "FullyQualifiedName~ConsoleLogTests.RingBuffer"` | ❌ W0 | ⬜ pending |
| 02-02-03 | 02 | 2 | PLYR-01 | — | Configuration synchronization | unit | `dotnet test --filter "FullyQualifiedName~PlayerListTests.Sync"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `PocketMC.Tests/Services/InstanceServiceTests.cs` — stubs for INST-01, INST-02
- [ ] `PocketMC.Tests/Services/ProcessRunnerTests.cs` — stubs for INST-04
- [ ] `PocketMC.Tests/Services/ConsoleLogTests.cs` — stubs for CONS-01
- [ ] `PocketMC.Tests/Services/PlayerListTests.cs` — stubs for PLYR-01

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Mojang API rate-limits/network fallback | PLYR-02 | Network dependent | Block Mojang API via local hosts file override and verify local cached UUID resolves properly. |

*If none: "All phase behaviors have automated verification."*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
