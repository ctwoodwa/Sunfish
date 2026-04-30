# FCRA Workflow

The Fair Credit Reporting Act (15 USC §1681 et seq.) governs how consumer reports are used in housing decisions. `Sunfish.Blocks.PropertyLeasingPipeline` ships an FCRA-compliant background-check + adverse-action workflow per [ADR 0057](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md).

## §604(b) — Written consent before procurement

The applicant must consent to a background check before the operator orders one. In Sunfish:

- The application form requires a signature in scope `consent-background-check` (per the [`Sunfish.Signature.Scopes@1.0.0`](../../../icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md) taxonomy).
- The signature is captured via `ISignatureCapture` and bound to the application via `Application.ApplicationSignature`.
- `ConfirmApplicationAndPromoteAsync` transitions to `AwaitingBackgroundCheck` only after the signature is present + the application fee is collected.

## §615(a) — Adverse-action notice

When an application is declined and the consumer report contributed to the decision, FCRA §615(a) requires the operator to issue a notice with mandatory content:

- Statement that the action was based on a consumer report
- Name + address of the consumer reporting agency
- Statement that the CRA did not make the decision + cannot explain it
- Notice of the consumer's right to obtain a free report from the CRA within 60 days
- Notice of the consumer's right to dispute accuracy

Sunfish ships this verbatim:

```csharp
public const string MandatoryFcraStatement =
    "We took adverse action on your application based in whole or in part on information " +
    "contained in a consumer report. The consumer reporting agency named below provided " +
    "the report; that agency did not make the decision and cannot explain why the decision " +
    "was made. You have the right to obtain, free of charge within 60 days of receiving " +
    "this notice, a copy of the report from the consumer reporting agency. You also have " +
    "the right to dispute the accuracy or completeness of any information in the report " +
    "directly with the agency.";
```

`FcraAdverseActionNoticeGenerator.Generate` builds the notice + applies the 60-day dispute window:

```csharp
var notice = generator.Generate(
    application: applicationId,
    findings: bgResult.Findings,
    cra: new ConsumerReportingAgencyInfo("ABC Reports Inc.", "123 Main St, Anytown, USA"),
    issuanceSignature: operatorSignatureRef);
```

## §612(a) — 60-day dispute window

`AdverseActionNotice.DisputeWindowExpiresAt` is computed at issuance: `IssuedAt + 60 days` by default. Some state laws extend the window (e.g., NY's source-of-income decisions get 90 days per `us-state.ny.adverse-action-extended-window`); the generator accepts a custom window via constructor:

```csharp
new FcraAdverseActionNoticeGenerator(time: timeProvider, disputeWindow: TimeSpan.FromDays(90));
```

## Pluggable background-check providers

`IBackgroundCheckProvider` is the contract; the first concrete adapter (e.g. `providers-checkr`, `providers-transunion`) is a follow-up package per ADR 0013. W#22 Phase 3 ships only the contract + `InMemoryBackgroundCheckProvider` for test/demo:

```csharp
var bg = new InMemoryBackgroundCheckProvider();
bg.SeedFindings("XXX-XX-1234", new[] { evictionFinding });   // canned outcome
bg.SeedError("XXX-XX-5678");                                  // canned error
```

`BackgroundCheckOutcome` enum: `Clear` / `HasFindings` / `Error`.

## Audit emission

`BackgroundCheckCompleted` emits when a result is recorded. The audit body includes `vendor_ref` + `outcome` + `finding_count` — **NOT the verbatim findings** (those live in the `BackgroundCheckResult` record itself; the audit reference points back). FCRA-protected information stays out of long-lived audit storage.

`AdverseActionNoticeIssued` is declared in kernel-audit but unwired pending a service-level notice-issuance op (Phase 8 follow-up).

## FCRA legal-review note

The `MandatoryFcraStatement` content mirrors the CFPB safe-harbor wording. **Production use should attorney-pass before customer rollout** — the safe-harbor language is the floor, not always the ceiling, and state-law addenda may apply. The hand-off names this as a halt-condition for production deployment.

## See also

- [ADR 0057](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — Leasing pipeline architecture
- [Jurisdiction Rules](./jurisdiction-rules.md) — FCRA + state-law extensions in the taxonomy
- [FHA Defense](./fha-defense.md) — Adjacent regulatory regime
