# Hand-off — Foundation.Recovery Package Split (ADR 0046 + ADR 0049 reconciliation)

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28
**Status:** `ready-to-build`
**Spec source:** ADR 0046 audit (`icm/07_review/output/adr-audits/0046-upf-audit.md` finding C-2) + UPF plan (in-conversation 2026-04-28 — Quality grade A; user approved option C "split by concern" 2026-04-28 with explicit "quality over cost" framing)
**Approval:** user said "c" 2026-04-28 (approved option C: split by concern, not atomic move)
**Estimated cost:** ~2-3 days sunfish-PM + ~1 hour research-session ADR-amendment review
**Pipeline:** `sunfish-api-change` (public namespace move from `Sunfish.Kernel.Security.Recovery` → `Sunfish.Foundation.Recovery`)

---

## Context (one paragraph)

ADR 0046 (key-loss recovery scheme) + ADR 0049 (audit-trail substrate) both reference `Sunfish.Foundation.Recovery` / `foundation-recovery`. The shipped Phase-1 implementation lives in `packages/kernel-security/Recovery/` (`Sunfish.Kernel.Security.Recovery` namespace). This is a kernel-vs-foundation tier mismatch on a security-critical primitive — paper §5's tier model treats this distinction as load-bearing.

The right architectural answer is **split by concern**, not atomic move: kernel-tier crypto primitives stay in kernel-security; foundation-tier orchestration moves to a new `packages/foundation-recovery/`. The implicit split already exists in kernel-security's folder structure (`Crypto/`, `Keys/` are clearly substrate; `Recovery/` is clearly orchestration over those primitives).

---

## Approach (UPF-graded A, quality-over-cost framing)

**Two-package outcome:**

```
packages/
├── kernel-security/
│   ├── Crypto/        ← Ed25519Signer, X25519KeyAgreement (KERNEL substrate; stay)
│   ├── Keys/          ← SqlCipherKeyDerivation, KeystoreRootSeedProvider, RotateKeyAsync (KERNEL substrate; stay)
│   └── Recovery/      ← REMOVED (or repurposed for kernel-tier-only recovery primitives if any genuinely emerge during inventory)
└── foundation-recovery/    ← NEW; receives the foundation-orchestration types
```

`foundation-recovery` references `kernel-security` (for Ed25519, X25519, key derivation, rekey) and `kernel-audit` (for `IAuditTrail` per ADR 0049 G6 host-integration target).

**Critical guard:** Phase 1 inventory is a research-session-reviewed gate before Phase 3 moves anything. Mis-classifying a type in either direction creates the problem this hand-off is trying to solve.

---

## Phases (binary gates)

### Phase 1 — Inventory + classification (research-reviewed gate)

Walk every type in `packages/kernel-security/Recovery/`. For each, classify as one of:

- **`foundation-orchestration`** — moves to `foundation-recovery`. Examples expected: `IRecoveryCoordinator`, `RecoveryRequest`, `TrusteeAttestation`, `RecoveryEvent`, grace-period timer, paper-key derivation orchestration (the BIP-39 *wrapper* that calls into kernel-security crypto)
- **`kernel-substrate`** — stays in kernel-security. Examples expected: any direct extensions of crypto primitives, low-level PRF wrappers, hash-chain-style helpers if present
- **`ambiguous`** — escalate to research session

**Deliverable for Phase 1:** a markdown table at `icm/_state/handoffs/adr-0046-recovery-package-split-INVENTORY.md` with one row per type, classification, and one-line rationale.

**PASS gate:** inventory committed; **research session reviews and confirms before Phase 3 begins**. If any type is `ambiguous`, halt and surface to research before proceeding.

### Phase 2 — Scaffold `packages/foundation-recovery/`

**Files:**

- **NEW** `packages/foundation-recovery/Sunfish.Foundation.Recovery.csproj`
  - Mirror existing `foundation-*` package csproj patterns (e.g., `packages/foundation-multitenancy/Sunfish.Foundation.MultiTenancy.csproj` is the closest precedent)
  - `<RootNamespace>Sunfish.Foundation.Recovery</RootNamespace>`
  - ProjectReferences: `..\foundation\Sunfish.Foundation.csproj`, `..\kernel-security\Sunfish.Kernel.Security.csproj`, `..\kernel-audit\Sunfish.Kernel.Audit.csproj`, `..\foundation-multitenancy\Sunfish.Foundation.MultiTenancy.csproj` (per ADR 0049 the audit records are tenant-scoped)
  - `InternalsVisibleTo` for the planned `Sunfish.Foundation.Recovery.Tests` project

- **MODIFY** `Sunfish.slnx` — add `packages/foundation-recovery/` to the solution

**PASS gate:** empty `foundation-recovery` package builds clean (`dotnet build packages/foundation-recovery/Sunfish.Foundation.Recovery.csproj` exits 0 with zero warnings).

### Phase 3 — Move types per Phase 1 inventory

Per the inventory's `foundation-orchestration` rows:

- Move source files from `packages/kernel-security/Recovery/` to `packages/foundation-recovery/`
- Update `namespace Sunfish.Kernel.Security.Recovery` → `namespace Sunfish.Foundation.Recovery` throughout
- Move corresponding test files from `packages/kernel-security/tests/Recovery/` (if that's the path) to a new `packages/foundation-recovery/tests/` project
- For any type classified `kernel-substrate` in Phase 1 — leave in place; foundation-recovery references kernel-security to consume them

**Watch for:**

- **Circular dependency**: foundation-recovery → kernel-security → ??? → foundation-recovery. If the build complains, Phase 1's classification is wrong. **HALT, escalate to research.**
- **Mis-named types**: if `Sunfish.Kernel.Security.Recovery.IRecoveryCoordinator` appears in the new namespace as `Sunfish.Foundation.Recovery.IRecoveryCoordinator`, that's correct. If it's still `Sunfish.Kernel.Security.Recovery.IRecoveryCoordinator` in the new physical location, the namespace replace was incomplete.

**PASS gate:** full repo `dotnet build` exits 0; full repo `dotnet test` reports all tests passing (some tests will need their `using` statements updated — that's part of this phase).

### Phase 4 — Update consumers

Inventory all consumers via repo-wide search (avoid Windows-line-ending pitfalls):

```bash
grep -rn "Sunfish.Kernel.Security.Recovery\|Kernel.Security.Recovery" --include="*.cs" --include="*.csproj" packages/ accelerators/ tooling/ apps/ 2>/dev/null
```

Expected sites (based on Phase 1 G6 progress per `project_business_mvp_phase_1_progress` memory):

- **Anchor `MauiProgram.cs`** — DI registration of recovery services. Update `using` statements; update DI registration namespace.
- **G6 host-integration code** — not yet started per the memory; if any preliminary work has landed, update references.
- **Any kernel-security tests** that depend on Recovery types — update `using` statements; if test belongs in foundation-recovery, move it.

**PASS gate:** repo-wide `dotnet build` exits 0; repo-wide `dotnet test` exits 0.

### Phase 5 — Amend ADR 0046 + ADR 0049 + roadmap

**Files:**

- **MODIFY** `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` — add a new section between **Decision** and **Consequences**:

  ```markdown
  ## Package placement (added 2026-04-28)

  The Phase-1 implementation is split across two packages per the paper's §5 tier model:

  - **`packages/kernel-security/`** — kernel-tier crypto primitives that recovery depends on:
    - `Crypto/Ed25519Signer.cs`, `Crypto/X25519KeyAgreement.cs` (signature primitives)
    - `Keys/SqlCipherKeyDerivation.cs`, `Keys/KeystoreRootSeedProvider.cs` (key derivation primitives)
    - `Keys/RotateKeyAsync` SQLCipher rekey primitive

  - **`packages/foundation-recovery/`** — foundation-tier orchestration over those primitives:
    - `IRecoveryCoordinator` + impl
    - `RecoveryRequest`, `TrusteeAttestation`, `RecoveryEvent` (data contracts)
    - Paper-key derivation orchestration (BIP-39 wrapper over kernel-security PRFs)
    - Grace-period timer
    - `IAuditTrail` integration per ADR 0049

  This split was confirmed during the 2026-04-28 ADR audit; the original ADR 0046 text
  (which referenced `Sunfish.Foundation.Recovery` for the entire scheme) was a planning-time
  guess made before the kernel-security substrate was complete. The shipped reality
  (split-by-concern) is the de-facto correct decision; this section ratifies it.
  ```

- **MODIFY** `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` — also fix any in-text references that still say `Sunfish.Foundation.Recovery` for the entire scheme; replace with the split.

- **MODIFY** `docs/adrs/0049-audit-trail-substrate.md` — patch the `Compatibility plan` and `Implementation checklist` references to `Foundation.Recovery`; replace with `Foundation.Recovery (orchestration; depends on Kernel.Security crypto primitives)`.

- **MODIFY** `docs/specifications/inverted-stack-package-roadmap.md` — `Sunfish.Foundation.Recovery` row:
  - `Status:` `book-committed` → `scaffolded`
  - `Sunfish dir (planned):` `packages/foundation-recovery/` → `Sunfish dir: packages/foundation-recovery/`
  - Add a one-line note that kernel-tier crypto primitives stay in `packages/kernel-security/`

**PASS gate:** all three docs updated; cross-references between ADR 0046 ↔ ADR 0049 ↔ roadmap consistent (no stray `Foundation.Recovery` reference that contradicts the split).

### Phase 6 — PR with auto-merge

PR title: `refactor(foundation-recovery): split recovery orchestration from kernel-security per ADR 0046 amendment`

PR body should reference:
- This hand-off file
- The Phase 1 inventory file (committed in same PR)
- ADR 0046 amendment + ADR 0049 amendment + roadmap update
- The UPF Quality A grade
- The audit finding C-2 in `CONSOLIDATED-HUMAN-REVIEW.md`

Auto-merge enabled per `feedback_pr_push_authorization`.

---

## Out of scope (explicit do-NOT-touch list)

- **Don't move `Crypto/` or `Keys/` content** — those are kernel-substrate; they stay.
- **Don't refactor type internals** — pure namespace + file-location move; no logic changes.
- **Don't touch ADR 0048 / ADR 0044** — Anchor multi-platform + Windows-only Phase 1 are unrelated.
- **Don't write `Foundation.Recovery` Tier 2 retrofit** — this is the package split only; further Foundation.Recovery scope is separate.
- **Don't optimize / consolidate** during the move — preserve the existing structure of files within the package.

---

## Acceptance criteria (PR-level)

- [ ] Phase 1 inventory committed at `icm/_state/handoffs/adr-0046-recovery-package-split-INVENTORY.md` and reviewed by research session
- [ ] `packages/foundation-recovery/Sunfish.Foundation.Recovery.csproj` exists; references `kernel-security`, `kernel-audit`, `foundation-multitenancy`
- [ ] Solution `Sunfish.slnx` includes `foundation-recovery`
- [ ] All foundation-orchestration types (per inventory) moved with namespace updated; tests follow
- [ ] `packages/kernel-security/Recovery/` either deleted (if Phase 1 says no kernel-substrate types remain) OR contains only the `kernel-substrate`-classified types
- [ ] `dotnet build` of full repo exits 0; `dotnet test` of full repo exits 0
- [ ] ADR 0046 has new "Package placement (added 2026-04-28)" section; in-text references corrected
- [ ] ADR 0049 `Compatibility plan` + `Implementation checklist` references patched
- [ ] `inverted-stack-package-roadmap.md` `Sunfish.Foundation.Recovery` row flipped to `scaffolded`
- [ ] PR auto-merge enabled
- [ ] On completion: `icm/_state/active-workstreams.md` workstream #15 → `built`

---

## Kill triggers (halt + report)

- **Phase 1 inventory finds an `ambiguous` type** → halt; surface to research session (e.g., a paper-key derivation that genuinely uses raw kernel-tier PRFs without orchestration could be either tier)
- **Circular dependency emerges in Phase 3** → halt; the split is wrong; revise inventory
- **Wall-clock burn exceeds 5 days** (~2× the 2-3 day estimate) → halt; reassess
- **External callers (outside this repo) discovered referencing `Sunfish.Kernel.Security.Recovery`** → halt; either keep the namespace alive as a type-forwarder shim OR open a deprecation cycle; surface to user

---

## Branch + PR strategy

If the GitButler workspace is congested per `feedback_use_worktree_when_gitbutler_blocks`, use the worktree workflow:

```bash
git fetch origin main
git worktree add /tmp/sunfish-recovery-split-wt origin/main -b refactor/foundation-recovery-package-split
```

Otherwise plain `but` or `git switch -c` works.

---

## On completion

1. Update `icm/_state/active-workstreams.md` workstream #15 → status `built`, link the merged PR
2. Optional project memory: `project_foundation_recovery_package_split_built.md` so research session sees the state change at next session-start
3. Audit finding **C-2** in `CONSOLIDATED-HUMAN-REVIEW.md` § 1 is **resolved** — note in any follow-up audit consolidation
4. The audit retrofit hand-off `kernel-audit-tier1-retrofit.md` may have references to `Foundation.Recovery` that need updating once this lands; cross-check and patch

---

## Notes for the research session reviewing Phase 1 inventory

When the inventory PR comes in for review:

- **Scan for ambiguous classifications** — anything where the rationale isn't crisp probably belongs in a follow-up rather than this PR's scope
- **Check for hidden kernel-tier dependencies** — if a type marked `foundation-orchestration` references something that ought to be private to kernel-security, the kernel-security side may need to expose a new internal-or-public surface
- **Verify the BIP-39 paper-key path** — this is the most likely ambiguity; the BIP-39 wordlist itself isn't a kernel primitive (it's a constant), but the derivation function that consumes the wordlist + a kernel-tier PRF is debatable; classify based on where the orchestration logic lives
