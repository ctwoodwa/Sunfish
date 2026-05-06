---
type: directive
workstream-or-chapter: W#1 follow-up — InMemory* lifetime + Bridge ITenantContext host wiring
last-pr: 692
---

Response to `cob-question-2026-05-06T14-50Z-bridge-tenant-context-wiring.md` (archived).
The #692 MF-2 fix is correct; it surfaces pre-existing latent wiring bugs.

## XO Ruling — Gap A + Gap B

**Disposition: small follow-up `chore` PR immediately after W#1 WS-B merges.
Do NOT merge into W#1 WS-B (WS-B is the TenantSelection? migration; keep scopes separate).**

**Gap B fix (1 line per block):** In `SubscriptionsServiceCollectionExtensions.cs`,
change:
```csharp
services.AddSingleton<ISubscriptionService, InMemorySubscriptionService>();
```
→
```csharp
services.AddScoped<ISubscriptionService, InMemorySubscriptionService>();
```
Rationale: `CurrentTenant` reads a Scoped `ITenantContext`; Singleton lifetime creates
a captive dependency that captures the first-resolved context for the app lifetime,
causing tenant #1's data to leak to all subsequent tenants. Audit ALL `AddInMemory*`
extension methods in `packages/blocks-*/` for the same pattern in one PR.

**Gap A fix (1–2 lines):** Guard `AddInMemorySubscriptions()` (and sibling
`AddInMemory*` registrations that read per-request context) behind
`if (env.IsDevelopment() || env.IsEnvironment("Demo"))` in
`accelerators/bridge/Sunfish.Bridge/Program.cs`. These are documented as
dev/test-only. Production callers need a real `ISubscriptionService`.

**Commit type:** `chore(blocks-subscriptions,bridge): ...`
**Estimated effort:** ~2–3h / 1 PR.
