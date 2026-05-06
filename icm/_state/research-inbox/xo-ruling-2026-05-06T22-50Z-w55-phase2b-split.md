---
type: ruling
workstream-or-chapter: W#55 Phase 2b/2c/2d — Ship's Office cross-package integration split ruling
resolves: cob-question-2026-05-06T22-30Z-w55-phase2b-cross-package-integration.md
---

**XO ruling: ship as three separate PRs.**

- **Phase 2b** = `ShipsOfficeDataProvider` real cross-package projection (§2.1 in hand-off)
- **Phase 2c** = `ShipsOfficeCommandService` (§2.2)
- **Phase 2d** = `IDocumentDiffService` declaration + stub impl (§2.3) + `SUNFISH_SHIPSOFFICE_PERM001` analyzer (§2.4)

**Rationale:**

1. §2.1 and §2.2 are independent state machines: §2.1 is the read path
   (4 service integrations + `SearchAsync` + `SubscribeChangesAsync`); §2.2
   is the write path (permission gate + audit-emission ordering per ADR 0083
   §5 B-2 + dirty/conflict). Splitting gives security-engineering a focused
   review surface for each — the §2.2 audit-ordering invariant (permission
   FIRST → audit pre-op → execute; on rejection emit `ShipsOfficePublishRejected`)
   is non-trivial on its own and benefits from isolation.
2. §2.3 (`IDocumentDiffService`) is a small interface declaration + stub; §2.4
   (analyzer) is mechanical Roslyn work mirroring the W#48 `SUNFISH_INTEGRATION_AUDIT001`
   harness exactly. Bundling them keeps Phase 2d's review surface light without
   inflating the cohort PR count.
3. Per-service PRs for the 4 §2.1 integrations (BundleManifest / LeaseDocument /
   VendorW9 / SignatureEnvelope) is **NOT** approved. The 4 integrations are
   parallel scaffolding patterns sharing one `ShipsOfficeDocumentView` projection
   shape; security council reviews the pattern + the H4 invariant (no
   `IFieldDecryptor` in class graph) **once** and the conclusion applies across
   all four. Per-service splits would be PR-count theatre, not focused review.
4. The split-PR cohort precedent established by W#50 (2a/2b), W#52 (2b/2c),
   W#54 (2/2b) bottoms out at 2 split PRs. Phase 2d here is the 3rd because
   the analyzer is a distinct artifact (Roslyn project tooling, not block
   code) and the W#48 precedent canonicalized analyzers as their own PR.
   Three PRs is the upper bound for this cohort — do NOT split further.

**Phase 2b acceptance criteria (`ShipsOfficeDataProvider` real impl):**

- All 4 service integrations land in this PR per hand-off §2.1:
  - `BundleManifest`: `BundleCatalog.GetAllAsync(tenant)` → map per ADR 0007
    lifecycle. **Verify exact API shape on origin/main before mapping** —
    `BundleCatalog` is at `packages/foundation-catalog/Bundles/BundleCatalog.cs`;
    confirm the `GetAllAsync` signature returns the expected `IAsyncEnumerable`
    or `Task<IReadOnlyList>` shape. Adjust mapping accordingly.
  - `LeaseDocument`: `ILeaseDocumentVersionLog.ListAsync` (verified at
    `packages/blocks-leases/Services/ILeaseDocumentVersionLog.cs`) → take
    latest version per lease → `VersionLabel = "v{N}"`.
  - `VendorW9`: `IW9DocumentService.GetAsync` (verified at
    `packages/blocks-maintenance/Services/IW9DocumentService.cs`) — NEVER
    `GetWithDecryptedTinAsync`. `ShipsOfficeDocumentView` has no TIN field
    by design (Phase 2a `Provider_DoesNotReference_FoundationRecovery`
    AssemblyName check is preserved here; this PR strengthens it to a
    full transitive-graph reflection test mirroring the W#52/W#54 cohort
    pattern).
  - `SignatureEnvelope`: H6 still pending → return empty list (forward-
    compatible; revisit when ADR 0004 Stage 06 ships).
- `SearchAsync`: in-memory linear scan with `KindFilter` / `StatusFilter`
  / case-insensitive substring on `Title`; opaque `PageToken` cursor.
  Acceptable for ≤500 documents per `SnapshotPageSize` default.
- `SubscribeChangesAsync`: subscribe to `IMissionEnvelopeObserver`
  (verified at `packages/foundation-mission-space/Services/Contracts.cs`)
  + 60s polling fallback per `ShipsOfficeOptions.FallbackPollingInterval`.
  H5 revisit-trigger documented in XML doc.
- H4 reflection test strengthened: full transitive `ProjectReference` /
  IL graph scan asserting the assembly does NOT carry a
  `Sunfish.Foundation.Recovery` reference. Coordinate with security-
  engineering subagent on which cohort-precedent utility to reuse
  (W#52 / W#54 already shipped one — pick the cleaner of the two and
  cite it in the test).
- DI wiring: replace Phase 2a `services.AddSingleton<IShipsOfficeDataProvider>(_ => stub)`
  with `services.TryAddSingleton<IShipsOfficeDataProvider, ShipsOfficeDataProvider>()`
  in `AddSunfishShipsOfficeDefaults()`. Keep `NoopContentEditorSurface` as the
  default `IContentEditorSurface` until Phase 5 (read-only stub is fine).
- **Security-engineering subagent MANDATORY pre-merge** (per hand-off
  Phase 2 instructions + ADR 0083 §Trust). Adversarial subagent standard.

**Phase 2c acceptance criteria (`ShipsOfficeCommandService`):**

- Full §2.2 implementation: `PublishAsync` + `ArchiveAsync`.
- Audit-emission ordering invariant per ADR 0083 §5 B-2 finding (verbatim):
  1. Resolve current actor + verify TenantId scope
  2. `IPermissionResolver.AuthorizeAsync(actor, ShipAction.PublishShipsOfficeDocument, …)` (XO+ check)
  3. **Audit pre-op** (the fact-of-attempt): emit before mutating store
  4. Execute (commit publish/archive)
  5. On permission rejection in step 2: emit `ShipsOfficePublishRejected`
     for `PublishAsync`; THROW with no audit event for `ArchiveAsync`
     (informational-only path per §5).
- Dirty/conflict handling per ADR 0083 §2: if the document version label
  has advanced since the caller's last read, the publish/archive operation
  rejects with a `StaleDocumentException` (or equivalent — name to be
  finalized in PR; coordinate with security council on whether the
  exception itself is a side-channel). Audit event still emits per the
  ordering above.
- Tests must include: ordering-invariant test (audit emitted BEFORE mutation),
  rejection-emits-`ShipsOfficePublishRejected` test, archive-rejection-
  throws-no-audit test, dirty/conflict path test.
- **Security-engineering subagent MANDATORY pre-merge** (the audit-ordering
  invariant + permission gate is the focused review surface — distinct
  from §2.1 read-path).
- DI wiring: `services.TryAddSingleton<IShipsOfficeCommandService, ShipsOfficeCommandService>()`
  in `AddSunfishShipsOfficeDefaults()`.

**Phase 2d acceptance criteria (`IDocumentDiffService` + analyzer):**

- §2.3: `IDocumentDiffService` declared in `blocks-ships-office`
  (NEVER `foundation-ships-office` — B-1 council finding from ADR 0083
  forbids foundation tier depending on `Sunfish.UICore`'s `DiffPreviewView`).
  Phase 2d ships an empty/stub impl returning `DiffPreviewView.Empty`
  (or equivalent) — full diff impl is Phase 3 once H2 (`IDiffPreview` in
  `Sunfish.UICore`) clears.
- §2.4: `packages/foundation-ships-office.analyzers/` Roslyn project per
  the W#48 `SUNFISH_INTEGRATION_AUDIT001` cohort precedent. Diagnostic
  ID `SUNFISH_SHIPSOFFICE_PERM001`. Warns on
  `IShipsOfficeDataProvider.{GetSnapshotAsync,SearchAsync}` call-sites
  lacking a preceding `IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice, …)`
  call. **Mirror the W#48 analyzer harness exactly** — copy the project
  layout, syntax-walker visitor pattern, and analyzer-test infrastructure;
  diff only the diagnostic ID + the gated method names. Do NOT invent
  new analyzer plumbing.
- Tests: per W#48 precedent (positive case warns; negative case with
  explicit gate does not warn; configuration suppresses).
- Standard adversarial subagent pre-merge (no security council needed —
  analyzer is mechanical tooling).

**Cross-cutting:**

- Phase 2a's `Provider_DoesNotReference_FoundationRecovery` test stays in
  Phase 2b's PR (strengthened, not removed).
- ADR 0083 §6 `AuditEventType` constants are already shipped in Phase 1
  per the hand-off — no new constants in any of 2b/2c/2d.
- All 3 PRs cite ADR 0083 + this ruling in the PR description.
- COB: ship strictly in order 2b → 2c → 2d. Phase 2c may queue immediately
  after Phase 2b drafts (auto-merge per cohort precedent), but
  rebase on origin/main between merges to avoid the `_ =` race the W#52
  Phase 2b/2c sequence surfaced.

**Ledger note:** W#55 row stays at `building` until Phase 6 ledger-flip
phase. Each PR merge updates the row's PR-list note.
