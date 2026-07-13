# Phase 1: Core Foundation & Provisioning - Context

**Gathered:** 2026-07-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Set up the dependency-injected hosting core (`Microsoft.Extensions.Hosting`), platform-specific secure credentials storage (Keychain/libsecret), system folders initialization, and automated background download engines for Java and PHP runtimes.
</domain>

<decisions>
## Implementation Decisions

### Secure Storage Fallback Policy
- **D-01:** Silent AES Fallback: If native platform stores (libsecret on Linux, Keychain on macOS) are unavailable, credentials are encrypted using AES with a machine-derived key silently, keeping the setup seamless.

### Java & PHP Runtime Provisioning Mode
- **D-02:** On-Demand Download: Only trigger a runtime installation when creating or starting a server instance that requires that version, minimizing startup delay and disk usage.

### Custom Root Storage Overrides
- **D-03:** Configurable Root Path: Keep settings.json in the default OS folder, but allow configuring an alternate data path for instances/backups/downloads in settings.json.

### Pre-Launch Binary Validation Action
- **D-04:** Error Block & Repair UI: Block server launch, present a detailed error dialog with troubleshooting info, and offer to reinstall the runtime or change the path.

### the agent's Discretion
- The agent has discretion over the choice of AES key derivation details (e.g. using a SHA-256 hash of the host computer's machine ID or MAC address) and the precise JSON schema structure for configuring alternate paths in settings.json.
- The agent also has discretion to select the appropriate download libraries or APIs in C# (e.g., standard HttpClient) for fetching Temurin runtimes and PHP binaries.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Scope & Goals
- `.planning/PROJECT.md` — Project definition, core value, and constraints.
- `.planning/REQUIREMENTS.md` — Detailed descriptions of HOST and PROV requirements.
- `.planning/ROADMAP.md` — Phase success criteria.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Greenfield setup: No reusable assets or components exist.

### Established Patterns
- Greenfield setup: No pre-existing codebase patterns established.

### Integration Points
- Greenfield setup: This phase establishes the foundation. All future phases will integrate with the DI services and layout established here.

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

*Phase: 1-Core Foundation & Provisioning*
*Context gathered: 2026-07-13*
