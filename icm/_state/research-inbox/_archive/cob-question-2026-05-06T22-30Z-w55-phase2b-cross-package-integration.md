---
type: question
workstream-or-chapter: W#55 Phase 2b — cross-package integration + CommandService + Diff + Analyzer
last-pr: 708
---

PR for W#55 Phase 2 ships **Phase 2a only** — `blocks-ships-office`
substrate with empty-snapshot `ShipsOfficeDataProvider` stub +
`NoopContentEditorSurface` + DI. Cohort split per W#50 / W#52 / W#54
precedent: ship the package skeleton, defer cross-package wiring.

**Phase 2b deferred (follow-up PRs):**

1. **`ShipsOfficeDataProvider` real cross-package projection** —
   integrate with:
   - `BundleCatalog.GetAllAsync(tenant)` → BundleManifest views
   - `ILeaseDocumentVersionLog.ListAsync` → LeaseDocument views
     (latest version per lease)
   - `IW9DocumentService.GetAsync` → VendorW9 views (NEVER
     `GetWithDecryptedTinAsync` — TIN excluded by design per H4)
   - `IMissionEnvelopeObserver` → push-driven invalidation
   Each integration needs API verification on origin/main; the
   hand-off §2.1 cites APIs that may have drifted.
2. **`SearchAsync` real implementation** — in-memory linear scan
   over the snapshot projection with KindFilter / StatusFilter /
   case-insensitive substring on Title; opaque PageToken cursor.
3. **`ShipsOfficeCommandService` (§2.2)** — full command-side
   write path with permission gate + audit emission + dirty/
   conflict handling per ADR 0083 §2.
4. **`IDocumentDiffService` declaration + stub impl (§2.3)** —
   per the hand-off this lives in `blocks-ships-office`, not
   foundation, due to the B-1 council tier-discipline finding.
5. **`SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer (§2.4)** — new
   `packages/foundation-ships-office.analyzers/` project per the
   W#48 SUNFISH_INTEGRATION_AUDIT001 cohort precedent. Diagnostic
   warns on `IShipsOfficeDataProvider.{GetSnapshotAsync,SearchAsync}`
   call-sites lacking a preceding `IPermissionResolver.AuthorizeAsync(
   ShipAction.ViewShipsOffice, ...)` call. Mirror the W#48 analyzer
   harness exactly.

**What would unblock me:** XO ruling on whether to ship the cross-
package integration as a single Phase 2b PR or split per service
(BundleManifest / LeaseDocument / VendorW9 / SignatureEnvelope each
in their own PR — gives security council a tighter review surface
similar to the W#52 P2a/2b/2c split). The Roslyn analyzer is a
natural separate PR per the W#48 precedent.

**H4 invariant status (Phase 2a):** trivially holds — the stub yields
zero documents and has no `IFieldDecryptor` reference. Phase 2b's
real impl MUST add the cohort H4 reflection test
(`Provider_DoesNotReference_FoundationRecovery` is shipped here as
a pre-emptive AssemblyName-level check).
