---
type: directive
workstream-or-chapter: W#1 follow-up — InMemory* lifetime + Bridge ITenantContext host wiring
last-pr: 692
---

Response to `cob-question-2026-05-06T14-50Z-bridge-tenant-context-wiring.md`.
The #692 MF-2 fix is correct; it surfaces a pre-existing latent wiring bug.

## XO Ruling — Gap A + Gap B

**Disposition: small follow-up `chore` PR immediately after #692 merges.
Do NOT merge into W#1 WS-B (WS-B is TenantSelection? migration; keep separate).**

**Gap B fix (1 line):** In `SubscriptionsServiceCollectionExtensions.cs`, change:
```csharp
services.AddSingleton<ISubscriptionService, InMemorySubscriptionService>();
```
→
```csharp
services.AddScoped<ISubscriptionService, InMemorySubscriptionService>();
```
Rationale: `CurrentTenant` reads a Scoped `ITenantContext`; Singleton lifetime creates
a captive dependency that silently captures the first-resolved context for the app
lifetime under `ValidateScopes`.

**Gap A fix (1–2 lines):** Guard `AddInMemorySubscriptions()` (and any sibling
`AddInMemory*` registrations that read per-request context) behind
`if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))`
in `accelerators/bridge/Sunfish.Bridge/Program.cs`. These are explicitly documented
as dev/test-only; production callers must register a real `ISubscriptionService`.
Do NOT delete the unconditional call without a production replacement — if one
doesn't exist yet, gate it behind a feature flag or leave a `// PRODUCTION-TODO` marker.

**Audit scope:** Check all `AddInMemory*` extension methods in `packages/blocks-*/`
for `AddSingleton` registrations that inject per-request context types. Flip each
to `AddScoped`. This can be one PR covering all affected blocks.

**Size:** ~4–8 file edits + tests that verify the scoped lifetime resolves correctly
in a DI container with `ValidateScopes = true`. ~2–3h. Commit as `chore(blocks): ...`.

PR #692 may proceed to merge with auto-merge. The Gap A/B defects are pre-existing
host-wiring issues unmasked by MF-2; the MF-2 change itself is correct.
