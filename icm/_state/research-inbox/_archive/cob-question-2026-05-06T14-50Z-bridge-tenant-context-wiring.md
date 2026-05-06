---
type: question
workstream-or-chapter: W#1 follow-up — Bridge ITenantContext wiring + AddInMemory* lifetimes
last-pr: 692
---

PR #692 council surfaced two pre-existing host-wiring gaps that were silently
masked by the old `TenantId.System` fallback in `InMemorySubscriptionService`.
Both are out-of-scope for #692 (which only ships the 6 MF-items per directive)
but need an XO ruling on disposition.

**Gap A — Bridge production host has no ITenantContext.**
`accelerators/bridge/Sunfish.Bridge/Program.cs` registers
`AddScoped<ITenantContext, DemoTenantContext>` only inside `if (IsDevelopment)`.
`AddInMemorySubscriptions()` runs unconditionally. Post-#692, prod hosts that
hit any tenant-scoped subscription path will throw `InvalidOperationException`
on first call. (Dev hosts also break: Scoped→Singleton captive-dependency under
`ValidateScopes`.) The throw is the correct fail-closed posture — but the host
needs a real ITenantContext registration before it can serve tenant-scoped
calls again. Council recommendation: separate workstream.

**Gap B — `AddInMemorySubscriptions` registers Singleton; should be Scoped.**
Now that `CurrentTenant` reads from a Scoped dependency, Singleton lifetime is
a captive-dependency anti-pattern. Sibling `AddInMemoryTenantAdmin*` blocks may
have the same mismatch. Council recommendation: audit all `AddInMemory*` blocks
for Scoped/Singleton-with-tenant mismatches in a follow-up workstream.

**What would unblock me:** ruling on (a) whether to scope this as a small W#1
follow-up or merge into W#1 WS-B, and (b) whether to audit all `AddInMemory*`
extensions in the same workstream or split per-block.
