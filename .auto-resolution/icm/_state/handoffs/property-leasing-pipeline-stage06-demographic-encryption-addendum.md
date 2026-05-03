# Workstream #22 — `DemographicProfile` retroactive `EncryptedField` wiring (post-W#32 substrate)

**Supersedes (specific clauses of):** [`property-leasing-pipeline-stage06-handoff.md`](./property-leasing-pipeline-stage06-handoff.md) §"Phase 6 audit half" + Phase 6 `LeasingPipelineService` audit-emission invariant test (which currently uses reflection-based field-name absence as a workaround for plaintext storage).
**Effective:** 2026-04-30 (resolves COB idle beacon `cob-idle-2026-04-30T16-00Z-priority-queue-dry` follow-up item #1)
**Spec source:** Closes the W#32 substrate → W#22 consumer chain. ADR 0057 §"FHA-defense layout" specifies that protected-class fields must be structurally inaccessible to decisioning; W#32 (`EncryptedField` + `IFieldDecryptor`) makes this structural rather than test-enforced.

W#22 ledger row is `built` (PR #340); this addendum specifies a follow-up phase that retroactively encrypts `DemographicProfile` per-field via the now-shipped W#32 substrate. **api-change pipeline** (Application + DemographicProfile contract change at v1.0 — but no production callers yet, so migration is trivial).

Per Decision Discipline Rule 3 (auto-accept mechanical amendments), this addendum is mechanical: every change is "wrap field `T` in `EncryptedField`" + corresponding decrypt-on-read flow at boundary callers. No new substrate concepts.

---

## Phase 9 — DemographicProfile field-encryption (~3-5h)

### Files to modify

#### 1. `packages/blocks-property-leasing-pipeline/Models/DemographicProfile.cs`

Migrate every protected-class field from plaintext to `Sunfish.Foundation.Recovery.EncryptedField`:

```csharp
namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

using Sunfish.Foundation.Recovery;

public sealed record DemographicProfile
{
    // Fields covered by FHA protected-class list (HUD §100.20-100.24):
    public required EncryptedField? Race { get; init; }                     // was: string? Race
    public required EncryptedField? Color { get; init; }                    // was: string? Color
    public required EncryptedField? NationalOrigin { get; init; }           // was: string? NationalOrigin
    public required EncryptedField? Religion { get; init; }                 // was: string? Religion
    public required EncryptedField? Sex { get; init; }                      // was: string? Sex
    public required EncryptedField? FamilialStatus { get; init; }           // was: string? FamilialStatus
    public required EncryptedField? Disability { get; init; }               // was: string? Disability

    // FCRA-adjacent + state-protected classes (sub-FHA federal but still protected):
    public required EncryptedField? Age { get; init; }                      // was: int?
    public required EncryptedField? MaritalStatus { get; init; }            // was: string?
    public required EncryptedField? SourceOfIncome { get; init; }           // was: string?
    public required EncryptedField? VeteranStatus { get; init; }            // was: string?

    // Non-protected metadata (not encrypted):
    public required DateTimeOffset CapturedAt { get; init; }
    public required ActorId CapturedBy { get; init; }
}
```

**Notes:**
- `Age`: stored as encrypted UTF-8 string (e.g., `"42"`); decrypts to a string then parses to `int`. Storing as `EncryptedField<int>` would require generic crypto-record support; out of W#22 scope.
- All fields nullable: a Prospect may decline to disclose. Decisioning code MUST treat `null` and `EncryptedField` (encrypted-empty) as equivalent (both = "withheld").
- `DemographicProfile` is created at form submission via `PublicInquiryRequest` form fields (W#28 Phase 5c-3); the W#28 inbound POST handler is the encryption point.

#### 2. Encryption point: `LeasingPipelineService.SubmitApplicationAsync`

Where the existing `LeasingPipelineService.SubmitApplicationAsync` accepts a `DemographicProfile` (currently plaintext from W#28 form), it now:

```csharp
public async Task<LeasingApplication> SubmitApplicationAsync(
    SubmitApplicationRequest request, CancellationToken ct)
{
    // Encrypt the demographic profile inline at the service boundary.
    var encryptedProfile = await EncryptDemographicProfileAsync(
        request.Demographics, request.Tenant, ct);

    // ... existing logic, but Demographics field is now EncryptedField-bearing ...
}

private async Task<DemographicProfile> EncryptDemographicProfileAsync(
    DemographicProfile plaintext,  // shape mismatch — see A.1 note below
    TenantId tenant,
    CancellationToken ct)
{
    // ... per-field encryption via _fieldEncryptor.EncryptAsync ...
}
```

**A.1 note (shape mismatch):** This is awkward — the in-memory record is now `EncryptedField`-typed but the wire-side `PublicInquiryRequest` carries plaintext. Two options:

- **Option A (preferred):** Introduce a `DemographicProfileSubmission` plaintext-shaped record at the `SubmitApplicationRequest` boundary. The `LeasingPipelineService.SubmitApplicationAsync` accepts `DemographicProfileSubmission` (plaintext) and produces a `DemographicProfile` (encrypted). Plaintext never persists.
- **Option B:** Keep `DemographicProfile` accepting plaintext OR encrypted (union via `discriminator` field). More complex; rejects.

**Choose Option A.** Add a new `DemographicProfileSubmission` record:

```csharp
public sealed record DemographicProfileSubmission
{
    public string? Race { get; init; }
    public string? Color { get; init; }
    public string? NationalOrigin { get; init; }
    public string? Religion { get; init; }
    public string? Sex { get; init; }
    public string? FamilialStatus { get; init; }
    public string? Disability { get; init; }
    public int? Age { get; init; }
    public string? MaritalStatus { get; init; }
    public string? SourceOfIncome { get; init; }
    public string? VeteranStatus { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required ActorId CapturedBy { get; init; }
}
```

`SubmitApplicationRequest.Demographics` becomes `DemographicProfileSubmission` (plaintext, transient). `LeasingApplication.Demographics` remains `DemographicProfile` (encrypted, persisted).

#### 3. Decryption point: where decisioning reads demographic fields

**It doesn't.** Per ADR 0057 §"FHA-defense layout," decisioning MUST NOT read demographic fields. The current audit-emission-invariant test enforces this via reflection on field names; with `EncryptedField`, the enforcement becomes structural — decisioning code reading `DemographicProfile.Race` gets `EncryptedField?` which is opaque without an `IFieldDecryptor` capability.

**Who decrypts:**
- **Compliance audit / HUD reporting tooling** — emits aggregated statistics (per ADR 0057's compliance reporting requirements). Holds an `IDecryptCapability` with audit-emitting decrypt; per-field decrypts emit `FieldDecrypted` audits per W#32.
- **Subject Access Request (SAR) handler** — when a Prospect requests their own data per GDPR / CCPA / FCRA §609, the handler decrypts to render. Holds an SAR-scoped `IDecryptCapability`.
- **No other readers.** The decisioning surface, AdverseActionNotice generator, and BackgroundCheck orchestrator all explicitly do NOT receive `DemographicProfile` references — they take only the non-protected `DecisioningFacts` per ADR 0057.

#### 4. Audit-emission invariant test refactor

The existing test in `LeasingPipelineServiceTests` currently uses reflection to assert that no `AuditPayload.Body` contains a key that matches a demographic field name (`race`, `color`, etc.). With Phase 9, this becomes redundant — the type system enforces that demographic fields can't reach audit emission without going through `IFieldDecryptor`, which would itself emit a `FieldDecrypted` audit (visible failure if accidentally invoked from decisioning).

**Two changes to the test:**
1. Add a positive structural assertion: a static analyzer (or unit-test reflection) verifies that decisioning code paths (`DecisioningFactsBuilder`, `BackgroundCheckOrchestrator`, etc.) do NOT statically reference `DemographicProfile.Race` (etc.) — accessing those properties would require a capability they don't have. This complements (does not replace) the audit-payload-shape check.
2. Keep the audit-payload field-name check, but downgrade severity from "primary structural defense" to "belt-and-braces tripwire" — useful as a regression detector.

#### 5. Migration semantics

**Production callers:** zero (W#22 v1.0 shipped 2026-04-30; no production deployments yet). No data migration required.

**In-flight test data:** existing in-memory test data uses plaintext `DemographicProfile`; tests are updated to use `DemographicProfileSubmission` at the input boundary. No persistent-store schema migration needed (in-memory provider).

**Future production callers:** Phase 2.2+ when the LLC ships a real Bridge production deployment, the `DemographicProfile`'s `EncryptedField`-bearing shape is what flows through. No backfill needed since v1.0 ships post-encryption-design.

#### 6. ADR 0057 amendment companion

This addendum is the consumer-side change; ADR 0057 itself needs an A1 amendment formalizing the structural enforcement claim:

**ADR 0057 A1 (separate PR; XO authors after this build lands):**
> "Phase 9 of W#22 (PR #N) replaces `DemographicProfile`'s per-field plaintext storage with `Sunfish.Foundation.Recovery.EncryptedField`. The FHA-defense layout's claim that protected-class fields are 'structurally inaccessible to decisioning' is now structurally enforced (the type system requires `IFieldDecryptor` capability to access plaintext, which decisioning code paths do not hold). The reflection-based audit-payload field-name absence check is retained as a belt-and-braces tripwire."

**Ordering:** the build PR lands first (this addendum's Phase 9); ADR 0057 A1 amendment lands as XO follow-up after the build is verified clean.

### Phase 9 acceptance criteria

- [ ] `DemographicProfile` record migrated to `EncryptedField`-bearing for all 11 protected-class + protected-adjacent fields
- [ ] `DemographicProfileSubmission` plaintext record added at `SubmitApplicationRequest` boundary
- [ ] `LeasingPipelineService.SubmitApplicationAsync` encrypts at the service boundary; plaintext never persists
- [ ] Tests:
  - 11 round-trip tests (one per field): submit → encrypt → retrieve → decrypt with capability → equals original
  - Capability rejection: read demographic field without capability → throws (caught by static-analyzer-equivalent runtime check)
  - Audit-emission invariant test refactored per A.1 §4
  - SAR handler smoke test: SAR-scoped capability decrypts all 11 fields + emits one `FieldDecrypted` audit per
  - HUD reporting smoke test: compliance-scoped capability decrypts in aggregate; one audit per field
- [ ] DI registration unchanged (W#32's `AddSunfishRecoveryCoordinator()` already provides `IFieldEncryptor` + `IFieldDecryptor`)
- [ ] All 56 existing W#22 tests still pass
- [ ] `DemographicProfile.CapturedAt` + `CapturedBy` unchanged (not protected fields)

**Effort:** ~3-5h sunfish-PM time. Build is mechanical given W#32 substrate is shipped.

**PR title:** `feat(blocks-property-leasing-pipeline)!: DemographicProfile EncryptedField wiring (W#22 Phase 9, post-W#32)`

### Halt-conditions for Phase 9

- **`SubmitApplicationRequest.Demographics` shape doesn't accept a `DemographicProfileSubmission` substitute cleanly** (e.g., the request is consumed by code that names `Demographics : DemographicProfile` reflectively): HALT + verify the actual contract; address inline if mechanical, otherwise file `cob-question-*-w22-p9-submission-shape.md`.
- **Static-analyzer enforcement (per A.1 §4 change 1) requires a Roslyn analyzer that doesn't yet exist**: HALT — defer the static check to a follow-up phase; ship the audit-payload tripwire as the only enforcement for Phase 9; XO authors a follow-up addendum for the analyzer.
- **`LeasingApplication` repository serialization** (EFCore mapping for `EncryptedField`) doesn't trivially compose: HALT + cross-reference W#32 hand-off A3.4 storage-shape note (three columns, NOT `OwnsOne`); apply the same pattern to `LeasingApplication.Demographics`.

### Out of scope for Phase 9

- Compliance reporting tool implementation (HUD aggregated-stats emitter; SAR fulfillment handler) — both are downstream consumers; this addendum specifies the surface they consume but doesn't ship them
- Static analyzer / Roslyn rule enforcing "decisioning code paths cannot statically reference `DemographicProfile.<protected>`" — defer to a follow-up phase if mechanical analyzer authoring isn't already in flight elsewhere
- Audit-event for "Prospect declined to disclose demographics" — orthogonal compliance question; not blocking

### Decision-class

This addendum is **session-class** per `feedback_decision_discipline` Rule 1 — within XO authority. Justification:

- **Compliance angle:** encrypting protected-class fields per HUD/FHA/FCRA pattern is uncontroversial good practice; not a CO judgment call (no business strategy or external-messaging implications)
- **api-change blast radius:** zero production callers (v1.0 just shipped); migration is trivial
- **Substrate alignment:** W#32 was authored partly to enable this; closes the substrate-consumer chain XO scoped in the W#23 hand-off (`EncryptedField` consumer set: W#18 Vendors TIN, W#22 Leasing Pipeline FCRA, W#23 iOS PII, ADR 0051 Payments)

Authority: XO; addendum follows the W#19 / W#21 / W#23 / W#28 addendum precedents.

---

## References

- COB idle beacon: `icm/_state/research-inbox/cob-idle-2026-04-30T16-00Z-priority-queue-dry.md` (filed 2026-04-30T16:00Z)
- W#22 hand-off: `icm/_state/handoffs/property-leasing-pipeline-stage06-handoff.md`
- W#32 substrate (consumed by Phase 9): `icm/_state/handoffs/adr-0046-a2-encrypted-field-stage06-handoff.md` + `addendum.md` + ADR 0046-A2/A3/A4/A5
- ADR 0057 (Leasing Pipeline + Fair Housing) — A1 amendment will be authored after Phase 9 build lands
- ADR 0049 (Audit Trail Substrate) — `FieldDecrypted` audit consumer
- W#22 v1.0 hand-off Phase 6 audit-emission-invariant test (refactored per A.1 §4)
- HUD §100.20-100.24 (FHA protected classes); FCRA §609 (Subject Access Right)
