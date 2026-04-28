# Hand-off — Kernel-Audit Tier 1 Retrofit

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28
**Status:** `ready-to-build`
**Spec:** `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md` § "Kernel-audit retrofit plan" → "Tier 1 retrofits"
**Affects:** PR #190 (kernel-audit scaffold) — landing this hand-off unblocks decision on whether to re-arm PR #190's auto-merge

---

## Context (one paragraph)

A parallel scaffolding session created `packages/kernel-audit/` against the older single-tenant v0 design before this session widened the multi-tenancy convention intake (introducing `TenantSelection`, refining the attestation shape, recommending substrate-trusts-attestations policy). Three drift points were identified. Two are fixable now without depending on the convention's M2 finalization (this hand-off). The third — `AuditQuery.TenantId → TenantSelection` — is **Tier 2** and waits for convention M2.

After reading `EventLogBackedAuditTrail.cs:71-79`, the substrate's verification logic is already correct (hybrid: substrate verifies the payload's `SignedOperation` envelope, trusts multi-party `AttestingSignatures`). The "drift" on the verification policy is purely an XML doc claim that overclaims, not an impl bug. So Tier 1 is small.

---

## Changes to make

### Change 1 — Add `AttestingSignature` record struct; update `AuditRecord` field type

**Why:** Multi-party attestations currently stored as `IReadOnlyList<Signature>` cannot be verified downstream — without a `PrincipalId` per signature, a compliance reviewer reading historical records has no way to look up the attesting key. Pair the two.

**Files:**

- **NEW** `packages/kernel-audit/AttestingSignature.cs`:

  ```csharp
  using Sunfish.Foundation.Crypto;

  namespace Sunfish.Kernel.Audit;

  /// <summary>
  /// A single attestation: a principal's signature over an audit record's
  /// canonical bytes. Element type of <see cref="AuditRecord.AttestingSignatures"/>.
  /// Pairing the principal with the signature lets downstream consumers
  /// (compliance reviewers, regulatory exports) look up the attesting key
  /// when verifying the attestation set.
  /// </summary>
  /// <param name="PrincipalId">The principal whose signature this is.</param>
  /// <param name="Signature">Ed25519 signature over the canonical bytes attesting principal endorses.</param>
  public readonly record struct AttestingSignature(PrincipalId PrincipalId, Signature Signature);
  ```

- **MODIFY** `packages/kernel-audit/AuditRecord.cs`:
  - Field: change `IReadOnlyList<Signature> AttestingSignatures` to `IReadOnlyList<AttestingSignature> AttestingSignatures`
  - XML doc on the `AttestingSignatures` parameter: clarify that each entry pairs a principal with its signature; multi-party attestations are caller-verified (not substrate-verified) in v0.

- **MODIFY** `packages/kernel-audit/tests/AuditTrailTests.cs`:
  - In the `SignedRecordAsync` helper (around line 52): `AttestingSignatures: Array.Empty<Signature>()` → `AttestingSignatures: Array.Empty<AttestingSignature>()`
  - Same substitution at any other test fixture site (~5 occurrences total per the test file's pattern).
  - **Add one new test:** roundtrip a record with two `AttestingSignature` entries and assert that `QueryAsync` preserves both `PrincipalId`s and both `Signature`s through the IEventLog → in-process state path.

### Change 2 — Fix `IAuditTrail.AppendAsync` XML doc + `AuditRecord` "Signing scope" remarks

**Why:** The interface XML doc currently claims `AppendAsync` "verifies all signatures before persistence" — the impl actually does hybrid verification (single-issuer payload envelope, not multi-party attestations). Inline comment at `EventLogBackedAuditTrail.cs:71-79` already describes this correctly; only the contract docstring is wrong.

**Files:**

- **MODIFY** `packages/kernel-audit/IAuditTrail.cs`:
  - Replace the third `<para>` block under interface remarks (the one starting "Signature verification.") with:

    ```xml
    /// <para>
    /// <b>Signature verification is hybrid in v0.</b>
    /// <see cref="AppendAsync"/> verifies the payload's
    /// <see cref="Sunfish.Foundation.Crypto.SignedOperation{T}"/> envelope
    /// (single-issuer Ed25519) and rejects records with invalid envelope
    /// signatures via <see cref="AuditSignatureException"/>. The multi-party
    /// <see cref="AuditRecord.AttestingSignatures"/> are NOT algorithmically
    /// verified at the kernel boundary in v0 — verification of attestations
    /// is the producer's responsibility (e.g., RecoveryCoordinator already
    /// verifies trustee attestations via TrusteeAttestation.Verify before
    /// constructing the AuditRecord). ADR 0049 §"Open questions" tracks
    /// promotion of attestation verification to a kernel-tier check.
    /// </para>
    ```

- **MODIFY** `packages/kernel-audit/AuditRecord.cs`:
  - Find the `<b>Signing scope.</b>` paragraph in the existing remarks block.
  - Update it to match the new docstring: substrate verifies the payload envelope; multi-party attestations are caller-verified.
  - On the `AttestingSignatures` parameter doc, add: "Multi-party attestations are caller-verified; the audit substrate stores them but does not verify them in v0. See `IAuditTrail` remarks."

---

## Out of scope (explicitly do not touch)

- **`AuditQuery.TenantId` migration to `TenantSelection`** — Tier 2; blocked on multi-tenancy convention M2 (workstream #1 in `active-workstreams.md`).
- **`EventLogBackedAuditTrail` impl logic** — already correct; no behavior change.
- **`AuditEventType`** — already correct (open record-struct discriminator).
- **`IAuditTrail.QueryAsync` signature** — leave as-is.
- **README.md** — minor optional addendum allowed but not required: a one-liner clarifying that `AttestingSignatures` are caller-verified.

---

## Acceptance criteria

- [ ] `dotnet build packages/kernel-audit/Sunfish.Kernel.Audit.csproj` — 0 warnings, 0 errors
- [ ] `dotnet build packages/kernel-audit/tests/tests.csproj` — 0 warnings, 0 errors
- [ ] `dotnet test packages/kernel-audit/tests/tests.csproj` — all existing tests + the new AttestingSignature roundtrip test pass
- [ ] No new analyzer warnings introduced (`-warnaserror` build path stays clean)

---

## Commit + PR

**Commit message draft:**

```
refactor(kernel-audit): retrofit AttestingSignature shape + verification docstring (Tier 1)

Two retrofits identified by the multi-tenancy type surface convention intake
2026-04-28 § "Kernel-audit retrofit plan":

1. AttestingSignatures field changed from IReadOnlyList<Signature> to
   IReadOnlyList<AttestingSignature> where AttestingSignature pairs
   PrincipalId with Signature. Without the principal, downstream
   compliance reviewers cannot look up the attesting key to verify
   historical records.

2. IAuditTrail.AppendAsync XML doc softened from "verifies all signatures"
   to a hybrid-policy description matching the actual impl: substrate
   verifies the payload's SignedOperation envelope; multi-party
   AttestingSignatures are caller-verified upstream. Inline comment at
   EventLogBackedAuditTrail.cs:71-79 was already correct; only the
   contract docstring overclaimed.

The Tier 2 retrofit (AuditQuery.TenantId → TenantSelection) is blocked on
the multi-tenancy convention's Stage 02 architecture decision and is
deliberately not part of this commit.

Hand-off spec: icm/_state/handoffs/kernel-audit-tier1-retrofit.md
```

**PR / branch strategy (UPDATED 2026-04-28 — PR #190 merged without retrofit):**

- PR #190 merged into `main` at 08:43Z with the drift unaddressed. **Force-push is no longer an option.**
- Open a new branch from `main`: `git switch -c refactor/kernel-audit-tier1-retrofit main` (or `but branch new ...`).
- Apply the two changes per the spec above.
- Verify build + tests locally.
- Open a follow-up PR titled `refactor(kernel-audit): retrofit AttestingSignature shape + verification docstring (Tier 1)`.
- Auto-merge: this is your call. The hand-off doesn't carry the auto-merge-decision-deferral context anymore — Tier 2 is still pending separately, but Tier 1 is internally complete and self-contained. Auto-merge-on-green is reasonable.
- If the GitButler virtual-branch workspace is congested (8+ active branches), see `feedback_use_worktree_when_gitbutler_blocks` for the worktree workaround.

**Sooner-the-better priority note:** Every new caller of `AuditRecord.AttestingSignatures` (e.g., Foundation.Recovery wiring per ADR 0046 G6, future payments / IRS export per Phase 2 ADRs 0051/0052) will adopt the drifted `IReadOnlyList<Signature>` shape and need updating later. Landing Tier 1 quickly minimizes that follow-up surface.

**On completion:** update `icm/_state/active-workstreams.md`:
- Workstream #2 → status `built`, link the merged commit / PR
- Workstream #3 (PR #190 row) → note Tier 1 retrofit complete; remaining drift is Tier 2 (AuditQuery → TenantSelection) blocked on workstream #1's M2.

Optional: write a memory note (`project_kernel_audit_tier1_retrofit_complete.md`) so the research session sees the state change at next session-start.

---

## Open questions for sunfish-PM

If anything in this hand-off is ambiguous, write a memory note asking the research session before proceeding. Specifically:

- The "Optional" addendum to README — judgement call; do or skip.
- The new test for AttestingSignature roundtrip — exact assertion shape may need refinement based on existing test conventions in `AuditTrailTests.cs`.
- If force-pushing to PR #190's branch breaks any in-flight review threads, fall back to a separate follow-up PR.
