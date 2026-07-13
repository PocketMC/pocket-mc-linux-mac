---
phase: 1
slug: core-foundation-provisioning
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-13
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / dotnet test |
| **Config file** | PocketMC.Tests/PocketMC.Tests.csproj |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.UnitTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.UnitTests"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-01-01 | 01 | 1 | HOST-01 | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.HostingTests"` | ✅ W0 | ✅ green |
| 01-01-02 | 01 | 1 | HOST-02, HOST-04 | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.StorageTests"` | ✅ W0 | ✅ green |
| 01-01-03 | 01 | 1 | HOST-03 | — | native OS secrets or AES | unit | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.SecureStorageTests"` | ✅ W0 | ✅ green |
| 01-02-01 | 02 | 2 | PROV-01 | — | N/A | integration | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.JavaProvisionerTests"` | ✅ W0 | ✅ green |
| 01-02-02 | 02 | 2 | PROV-02, SEC-01 | — | zip-slip protection | integration | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.PHPProvisionerTests"` | ✅ W0 | ✅ green |
| 01-02-03 | 02 | 2 | PROV-03 | — | N/A | integration | `dotnet test --filter "FullyQualifiedName~PocketMC.Tests.VerificationTests"` | ✅ W0 | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `PocketMC.Tests/PocketMC.Tests.csproj` — test project setup with xunit
- [x] `PocketMC.Tests/HostingTests.cs` — stubs for HOST-01
- [x] `PocketMC.Tests/StorageTests.cs` — stubs for HOST-02, HOST-04
- [x] `PocketMC.Tests/SecureStorageTests.cs` — stubs for HOST-03
- [x] `PocketMC.Tests/JavaProvisionerTests.cs` — stubs for PROV-01
- [x] `PocketMC.Tests/PHPProvisionerTests.cs` — stubs for PROV-02, SEC-01
- [x] `PocketMC.Tests/VerificationTests.cs` — stubs for PROV-03

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Pre-launch validation failure warning | PROV-03 | UI rendering behavior | Run mocked fail-binary launch script; verify error block popup appears; click Reinstall. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved
