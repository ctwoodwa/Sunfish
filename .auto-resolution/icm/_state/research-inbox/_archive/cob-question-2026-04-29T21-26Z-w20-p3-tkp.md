---
type: question
workstream: 20
last-pr: 276
---

W#20 Phases 1+2 shipped (PRs #273, #276 in flight). Phase 3 (HmacThreadTokenIssuer) halts on the explicitly-named halt condition: `Sunfish.Foundation.Recovery.ITenantKeyProvider` doesn't exist in source on origin/main (only referenced in ADR 0052 + this hand-off). HMAC requires per-tenant key material to mint + verify tokens.

What would unblock me: addendum picking shape per the W#19 Phase 0 minimal-stub pattern — recommend (a) inline-introduce a minimal `ITenantKeyProvider` stub in `packages/foundation-recovery/` that returns deterministic per-tenant key bytes (e.g., HKDF over tenantId + a fixed seed; ADR 0046 Stage 06 will replace with real tenant-key derivation). Same Option-A class as Money/ThreadId/SignatureEventRef stubs already shipped. Pivoting to W#21 Signatures while waiting.
