namespace Sunfish.Kernel.Audit;

/// <summary>
/// Discriminator for the kind of event captured in an <see cref="AuditRecord"/>.
/// </summary>
/// <remarks>
/// <para>
/// Strings rather than an enum so blocks (recovery, capabilities, payments)
/// can introduce new event types without a kernel-tier coordination round.
/// Per ADR 0049 §"Open questions" — the v0 set focuses on Phase 1 G6 recovery
/// and the Phase 2 commercial scope (payments, IRS export, bookkeeper /
/// tax-advisor delegation). The list grows as compliance use cases surface.
/// </para>
/// <para>
/// <b>Naming convention:</b> <c>{Subject}{Verb}</c> in PascalCase.
/// </para>
/// </remarks>
public readonly record struct AuditEventType(string Value)
{
    // ===== ADR 0046 sub-pattern #48f — recovery audit trail =====

    /// <summary>An owner initiated a multi-sig recovery request.</summary>
    public static readonly AuditEventType KeyRecoveryInitiated = new("KeyRecoveryInitiated");

    /// <summary>A trustee submitted an attestation for a recovery request.</summary>
    public static readonly AuditEventType KeyRecoveryAttested = new("KeyRecoveryAttested");

    /// <summary>A holder of the original keys filed a dispute against a recovery request during the grace window.</summary>
    public static readonly AuditEventType KeyRecoveryDisputed = new("KeyRecoveryDisputed");

    /// <summary>A recovery request reached quorum + grace expiry and the new key took effect.</summary>
    public static readonly AuditEventType KeyRecoveryCompleted = new("KeyRecoveryCompleted");

    /// <summary>A trustee designation or revocation was applied to the trustee set.</summary>
    public static readonly AuditEventType TrusteeSetChanged = new("TrusteeSetChanged");

    // ===== Phase 2 commercial scope (placeholders; concrete payloads land
    //       when each subsystem ships) =====

    /// <summary>A capability was delegated from one principal to another.</summary>
    public static readonly AuditEventType CapabilityDelegated = new("CapabilityDelegated");

    /// <summary>A previously-delegated capability was revoked.</summary>
    public static readonly AuditEventType CapabilityRevoked = new("CapabilityRevoked");

    /// <summary>A payment was authorized (pre-capture).</summary>
    public static readonly AuditEventType PaymentAuthorized = new("PaymentAuthorized");

    /// <summary>An authorized payment was captured (funds moved).</summary>
    public static readonly AuditEventType PaymentCaptured = new("PaymentCaptured");

    /// <summary>A captured payment was refunded.</summary>
    public static readonly AuditEventType PaymentRefunded = new("PaymentRefunded");

    /// <summary>A bookkeeper delegate accessed financial records.</summary>
    public static readonly AuditEventType BookkeeperAccess = new("BookkeeperAccess");

    /// <summary>A tax-advisor delegate accessed financial records.</summary>
    public static readonly AuditEventType TaxAdvisorAccess = new("TaxAdvisorAccess");

    /// <summary>An IRS-format export was generated for a tax period.</summary>
    public static readonly AuditEventType IrsExportGenerated = new("IrsExportGenerated");

    // ===== ADR 0056 — Foundation.Taxonomy substrate =====

    /// <summary>A new taxonomy definition was created (Authoritative or Civilian regime).</summary>
    public static readonly AuditEventType TaxonomyDefinitionCreated = new("TaxonomyDefinitionCreated");

    /// <summary>A taxonomy version was published.</summary>
    public static readonly AuditEventType TaxonomyVersionPublished = new("TaxonomyVersionPublished");

    /// <summary>A taxonomy version was retired.</summary>
    public static readonly AuditEventType TaxonomyVersionRetired = new("TaxonomyVersionRetired");

    /// <summary>A node was added to a taxonomy version.</summary>
    public static readonly AuditEventType TaxonomyNodeAdded = new("TaxonomyNodeAdded");

    /// <summary>A node's display label / description was revised.</summary>
    public static readonly AuditEventType TaxonomyNodeDisplayRevised = new("TaxonomyNodeDisplayRevised");

    /// <summary>A node was tombstoned (soft-deleted).</summary>
    public static readonly AuditEventType TaxonomyNodeTombstoned = new("TaxonomyNodeTombstoned");

    /// <summary>A taxonomy definition was cloned (Civilian-regime derivation).</summary>
    public static readonly AuditEventType TaxonomyDefinitionCloned = new("TaxonomyDefinitionCloned");

    /// <summary>A taxonomy definition was extended (parent-relationship preserving derivation).</summary>
    public static readonly AuditEventType TaxonomyDefinitionExtended = new("TaxonomyDefinitionExtended");

    /// <summary>A taxonomy definition was altered (semantic-divergence derivation; requires explicit reason).</summary>
    public static readonly AuditEventType TaxonomyDefinitionAltered = new("TaxonomyDefinitionAltered");

    // ===== ADR 0053 — Work Orders =====

    /// <summary>A work order was created in the Draft state.</summary>
    public static readonly AuditEventType WorkOrderCreated = new("WorkOrderCreated");

    /// <summary>A work order transitioned Draft → Sent.</summary>
    public static readonly AuditEventType WorkOrderSent = new("WorkOrderSent");

    /// <summary>A work order transitioned Sent → Accepted.</summary>
    public static readonly AuditEventType WorkOrderAccepted = new("WorkOrderAccepted");

    /// <summary>A work order transitioned Accepted → Scheduled.</summary>
    public static readonly AuditEventType WorkOrderScheduled = new("WorkOrderScheduled");

    /// <summary>A work order transitioned Scheduled → InProgress.</summary>
    public static readonly AuditEventType WorkOrderStarted = new("WorkOrderStarted");

    /// <summary>A work order transitioned InProgress → OnHold.</summary>
    public static readonly AuditEventType WorkOrderHeld = new("WorkOrderHeld");

    /// <summary>A work order transitioned OnHold → InProgress.</summary>
    public static readonly AuditEventType WorkOrderResumed = new("WorkOrderResumed");

    /// <summary>A work order transitioned InProgress → Completed.</summary>
    public static readonly AuditEventType WorkOrderCompleted = new("WorkOrderCompleted");

    /// <summary>A work order transitioned Completed → AwaitingSignOff (or skipped to Invoiced).</summary>
    public static readonly AuditEventType WorkOrderSignedOff = new("WorkOrderSignedOff");

    /// <summary>A work order transitioned to the Invoiced state.</summary>
    public static readonly AuditEventType WorkOrderInvoiced = new("WorkOrderInvoiced");

    /// <summary>A work order transitioned Invoiced → Paid.</summary>
    public static readonly AuditEventType WorkOrderPaid = new("WorkOrderPaid");

    /// <summary>A work order transitioned to the Disputed state.</summary>
    public static readonly AuditEventType WorkOrderDisputed = new("WorkOrderDisputed");

    /// <summary>A work order transitioned to the Closed (final terminal) state.</summary>
    public static readonly AuditEventType WorkOrderClosed = new("WorkOrderClosed");

    /// <summary>A work order was cancelled (terminal-from-anywhere-pre-Closed).</summary>
    public static readonly AuditEventType WorkOrderCancelled = new("WorkOrderCancelled");

    /// <summary>A right-of-entry notice was recorded against a work order.</summary>
    public static readonly AuditEventType WorkOrderEntryNoticeRecorded = new("WorkOrderEntryNoticeRecorded");

    /// <summary>An appointment slot was proposed against a work order.</summary>
    public static readonly AuditEventType WorkOrderAppointmentScheduled = new("WorkOrderAppointmentScheduled");

    /// <summary>A previously-proposed appointment was confirmed.</summary>
    public static readonly AuditEventType WorkOrderAppointmentConfirmed = new("WorkOrderAppointmentConfirmed");

    /// <summary>A signature-bound completion attestation was captured.</summary>
    public static readonly AuditEventType WorkOrderCompletionAttestationCaptured = new("WorkOrderCompletionAttestationCaptured");

    // ===== ADR 0028 / ADR 0054 — Leases =====

    /// <summary>A lease was created in the Draft phase.</summary>
    public static readonly AuditEventType LeaseDrafted = new("LeaseDrafted");

    /// <summary>A new revision of the lease document was appended to the version log.</summary>
    public static readonly AuditEventType LeaseDocumentVersionAppended = new("LeaseDocumentVersionAppended");

    /// <summary>A party (tenant or co-leaseholder) recorded a signature on the lease.</summary>
    public static readonly AuditEventType LeasePartySignatureRecorded = new("LeasePartySignatureRecorded");

    /// <summary>The landlord attestation was bound to the lease.</summary>
    public static readonly AuditEventType LeaseLandlordAttestationSet = new("LeaseLandlordAttestationSet");

    /// <summary>A lease transitioned AwaitingSignature → Executed (all required signatures captured).</summary>
    public static readonly AuditEventType LeaseExecuted = new("LeaseExecuted");

    /// <summary>A lease transitioned Executed → Active (commencement date reached).</summary>
    public static readonly AuditEventType LeaseActivated = new("LeaseActivated");

    /// <summary>A lease was renewed (Active → Renewed).</summary>
    public static readonly AuditEventType LeaseRenewed = new("LeaseRenewed");

    /// <summary>A lease was terminated (terminal).</summary>
    public static readonly AuditEventType LeaseTerminated = new("LeaseTerminated");

    /// <summary>A lease was cancelled before execution (terminal).</summary>
    public static readonly AuditEventType LeaseCancelled = new("LeaseCancelled");

    // ===== ADR 0057 — Leasing pipeline =====

    /// <summary>An inquiry passed validation and was persisted at the public-input boundary.</summary>
    public static readonly AuditEventType InquiryAccepted = new("InquiryAccepted");

    /// <summary>An inquiry was rejected at the public-input boundary (validation failure).</summary>
    public static readonly AuditEventType InquiryRejected = new("InquiryRejected");

    /// <summary>An inquiry was promoted to a Prospect via the email-verification flow.</summary>
    public static readonly AuditEventType ProspectPromoted = new("ProspectPromoted");

    /// <summary>A Prospect was promoted to an Applicant after fee + signature confirmation.</summary>
    public static readonly AuditEventType ApplicantPromoted = new("ApplicantPromoted");

    /// <summary>A rental application was submitted by a Prospect.</summary>
    public static readonly AuditEventType ApplicationSubmitted = new("ApplicationSubmitted");

    /// <summary>A rental application was accepted by the operator.</summary>
    public static readonly AuditEventType ApplicationAccepted = new("ApplicationAccepted");

    /// <summary>A rental application was declined by the operator.</summary>
    public static readonly AuditEventType ApplicationDeclined = new("ApplicationDeclined");

    /// <summary>A rental application was withdrawn before decision.</summary>
    public static readonly AuditEventType ApplicationWithdrawn = new("ApplicationWithdrawn");

    /// <summary>A background-check provider was asked to begin a report (kickoff).</summary>
    public static readonly AuditEventType BackgroundCheckRequested = new("BackgroundCheckRequested");

    /// <summary>A background-check provider returned a final report.</summary>
    public static readonly AuditEventType BackgroundCheckCompleted = new("BackgroundCheckCompleted");

    /// <summary>An FCRA §615-compliant adverse-action notice was issued.</summary>
    public static readonly AuditEventType AdverseActionNoticeIssued = new("AdverseActionNoticeIssued");

    /// <summary>A leasing-pipeline-tier capability (Anonymous / Prospect / Applicant) was revoked.</summary>
    public static readonly AuditEventType LeasingPipelineCapabilityRevoked = new("LeasingPipelineCapabilityRevoked");

    // ===== ADR 0054 — Signatures =====

    /// <summary>A new SignatureEvent was captured (after consent + scope validation passed).</summary>
    public static readonly AuditEventType SignatureCaptured = new("SignatureCaptured");

    /// <summary>An append-only revocation entry was added for a SignatureEvent (per ADR 0054 A4+A5).</summary>
    public static readonly AuditEventType SignatureRevoked = new("SignatureRevoked");

    /// <summary>A signature-validity projection was re-computed; emitted on each consult of <c>GetCurrentValidityAsync</c>.</summary>
    public static readonly AuditEventType SignatureValidityProjected = new("SignatureValidityProjected");

    /// <summary>A UETA / E-SIGN consent record was recorded for a principal.</summary>
    public static readonly AuditEventType ConsentRecorded = new("ConsentRecorded");

    /// <summary>A previously-recorded consent was revoked (subsequent SignatureCaptured calls for that principal will be refused until a new consent is recorded).</summary>
    public static readonly AuditEventType ConsentRevoked = new("ConsentRevoked");

    // ===== ADR 0058 — Vendor onboarding =====

    /// <summary>A new <c>Vendor</c> record was created in the Pending onboarding state.</summary>
    public static readonly AuditEventType VendorCreated = new("VendorCreated");

    /// <summary>A magic-link token was issued for vendor onboarding (W#18 Phase 5).</summary>
    public static readonly AuditEventType VendorMagicLinkIssued = new("VendorMagicLinkIssued");

    /// <summary>A previously-issued vendor magic-link was consumed by the vendor (W#18 Phase 5).</summary>
    public static readonly AuditEventType VendorMagicLinkConsumed = new("VendorMagicLinkConsumed");

    /// <summary>A vendor's onboarding state transitioned (e.g., Pending → W9Requested).</summary>
    public static readonly AuditEventType VendorOnboardingStateChanged = new("VendorOnboardingStateChanged");

    /// <summary>A W-9 document was received from the vendor (W#18 Phase 4).</summary>
    public static readonly AuditEventType W9DocumentReceived = new("W9DocumentReceived");

    /// <summary>An operator verified a previously-received W-9 document.</summary>
    public static readonly AuditEventType W9DocumentVerified = new("W9DocumentVerified");

    /// <summary>A vendor was activated (operationally usable for work-order assignment).</summary>
    public static readonly AuditEventType VendorActivated = new("VendorActivated");

    // ===== ADR 0059 — Public Listings =====

    /// <summary>A <c>PublicListing</c> transitioned to the Published status (visible to anonymous browsers).</summary>
    public static readonly AuditEventType PublicListingPublished = new("PublicListingPublished");

    /// <summary>A <c>PublicListing</c> transitioned to the Unlisted status (no longer publicly visible).</summary>
    public static readonly AuditEventType PublicListingUnlisted = new("PublicListingUnlisted");

    // Note: InquiryAccepted + InquiryRejected are reused from the W#22 ADR 0057 set above —
    // both workstreams deal with the same lifecycle event at the same boundary
    // (public-listings inquiry-form post → leasing-pipeline IPublicInquiryService.SubmitInquiryAsync).

    /// <summary>An inquiry was submitted via the public-listing surface (after 5-layer defense passes; pre-leasing-pipeline persistence).</summary>
    public static readonly AuditEventType InquirySubmitted = new("InquirySubmitted");

    /// <summary>A capability was promoted Anonymous → Prospect (per ADR 0043 addendum + ADR 0059).</summary>
    public static readonly AuditEventType CapabilityPromotedToProspect = new("CapabilityPromotedToProspect");

    /// <summary>A capability was promoted Prospect → Applicant (per ADR 0043 addendum + ADR 0059; downstream of W#22 Phase 2 ConfirmApplicationAndPromote).</summary>
    public static readonly AuditEventType CapabilityPromotedToApplicant = new("CapabilityPromotedToApplicant");

    // ===== ADR 0046-A4/A5 — Field-encryption substrate (W#32) =====

    /// <summary>An <c>EncryptedField</c> value was decrypted via <c>IFieldDecryptor</c>.</summary>
    public static readonly AuditEventType FieldDecrypted = new("FieldDecrypted");

    /// <summary>An <c>EncryptedField</c> decrypt was rejected (capability invalid, ciphertext truncated, AES-GCM tag failure, or unsupported key version).</summary>
    public static readonly AuditEventType FieldDecryptionDenied = new("FieldDecryptionDenied");

    // ===== ADR 0059 — Prospect-capability verifier (W#28 Phase 5c-4) =====

    /// <summary>A Prospect-tier macaroon capability was verified successfully.</summary>
    public static readonly AuditEventType ProspectCapabilityVerified = new("ProspectCapabilityVerified");

    /// <summary>A Prospect-tier macaroon capability verification was rejected (decode failed / signature mismatch / wrong tenant / listing not allowed / email not verified / expired).</summary>
    public static readonly AuditEventType ProspectCapabilityDenied = new("ProspectCapabilityDenied");

    /// <summary>A Prospect (with verified capability) submitted a rental application via the public-listing capability-tier route (W#28 P5c-4 Slice C).</summary>
    public static readonly AuditEventType ProspectStartedApplication = new("ProspectStartedApplication");

    /// <summary>A verified Prospect capability resolved an email that has no matching Prospect entity — capability/data inconsistency (W#28 P5c-4 Slice C).</summary>
    public static readonly AuditEventType ProspectLookupOrphan = new("ProspectLookupOrphan");

    // ===== ADR 0028-A6/A7 — Foundation.Versioning federation handshake (W#34) =====

    /// <summary>A federation handshake's <c>VersionVector</c> evaluation produced an Incompatible verdict; the peer was rejected. Emission is dedup'd 1-per-(remote_node_id, failed_rule, failed_rule_detail) per 1-hour rolling window per A7.4.</summary>
    public static readonly AuditEventType VersionVectorIncompatibilityRejected = new("VersionVectorIncompatibilityRejected");

    /// <summary>A legacy device reconnected with kernel-minor-lag exceeding the compatibility window; the up-to-date peer entered one-sided receive-only mode per A6.5. Emission is dedup'd 1-per-(remote_node_id, kernel_minor_lag) per 24-hour rolling window per A7.4.</summary>
    public static readonly AuditEventType LegacyDeviceReconnected = new("LegacyDeviceReconnected");

    // ===== ADR 0028-A5/A8 — Foundation.Migration cross-form-factor (W#35) =====

    /// <summary>A host's hardware-tier profile changed (storage / network / sensor / power / adapter / manual reprofile) per A5.3.</summary>
    public static readonly AuditEventType HardwareTierChanged = new("HardwareTierChanged");

    /// <summary>A plaintext-readable record was UI-hidden because the form factor lacks the feature surface to display it (A8.3 rule 5).</summary>
    public static readonly AuditEventType PlaintextSequestered = new("PlaintextSequestered");

    /// <summary>An encrypted record was held in ciphertext-only state because the form factor lacks the cryptographic capability to decrypt it (A8.3 rule 5).</summary>
    public static readonly AuditEventType CiphertextSequestered = new("CiphertextSequestered");

    /// <summary>A previously-sequestered record returned to active visibility on derived-surface expansion (A5.4 rule 2).</summary>
    public static readonly AuditEventType DataReleased = new("DataReleased");

    /// <summary>A CP-class record was sequestered on a host whose form factor cannot read it; the host's vote is ineligible for that record's quorum (A8.3 rule 6).</summary>
    public static readonly AuditEventType FormFactorQuorumIneligible = new("FormFactorQuorumIneligible");

    /// <summary>A field-level write was rejected at the local CRDT-write boundary because the form factor lacks the per-tenant key for that field (A8.5 rule 6).</summary>
    public static readonly AuditEventType FieldWriteSequestered = new("FieldWriteSequestered");

    /// <summary>Adapter version was downgraded; A5.6 sequestration applied. Emission is dedup'd 1-per-(node_id, adapter_id, version_pair) per 6-hour rolling window per A8.7.</summary>
    public static readonly AuditEventType AdapterRollbackDetected = new("AdapterRollbackDetected");

    /// <summary>A new <c>FormFactorProfile</c> (foundation-migration) was provisioned on a host — initial profile detection or A5.7 enrollment.</summary>
    public static readonly AuditEventType FormFactorProvisioned = new("FormFactorProvisioned");

    /// <summary>A host completed the A5.7 QR-onboarding form-factor enrollment handshake.</summary>
    public static readonly AuditEventType FormFactorEnrollmentCompleted = new("FormFactorEnrollmentCompleted");

    /// <summary>An event referencing a schema epoch from before the host's compatibility window per A7.5.3.</summary>
    public static readonly AuditEventType LegacyEpochEvent = new("LegacyEpochEvent");

    // ===== ADR 0031-A1+A1.12 — Bridge → Anchor subscription-event-emitter (W#36) =====

    /// <summary>Bridge emitted a subscription event (one per attempt; pre-delivery).</summary>
    public static readonly AuditEventType BridgeSubscriptionEventEmitted = new("BridgeSubscriptionEventEmitted");

    /// <summary>Bridge-side delivery succeeded (HTTP 200 from Anchor).</summary>
    public static readonly AuditEventType BridgeSubscriptionEventDelivered = new("BridgeSubscriptionEventDelivered");

    /// <summary>Bridge-side retryable failure (HTTP non-200 / timeout / network error). Dedup'd 1-per-(tenant_id, event_id) per 1-hour window.</summary>
    public static readonly AuditEventType BridgeSubscriptionEventDeliveryFailed = new("BridgeSubscriptionEventDeliveryFailed");

    /// <summary>Bridge exhausted all 7 retry attempts; event moved to dead-letter queue. Security-relevant; no dedup.</summary>
    public static readonly AuditEventType BridgeSubscriptionEventDeliveryFailedTerminal = new("BridgeSubscriptionEventDeliveryFailedTerminal");

    /// <summary>An Anchor registered a webhook URL with Bridge per A1.4.</summary>
    public static readonly AuditEventType BridgeSubscriptionWebhookRegistered = new("BridgeSubscriptionWebhookRegistered");

    /// <summary>Per A1.12.1 — Bridge staged a 90-day shared-secret rotation (24-hour grace window during which both old + new secrets are accepted).</summary>
    public static readonly AuditEventType BridgeSubscriptionWebhookRotationStaged = new("BridgeSubscriptionWebhookRotationStaged");

    /// <summary>Per A1.12.3 — Bridge admin enabled per-Anchor self-signed cert allowance for webhook delivery (default is publicly-rooted CA verification).</summary>
    public static readonly AuditEventType BridgeWebhookSelfSignedCertsConfigured = new("BridgeWebhookSelfSignedCertsConfigured");

    /// <summary>Anchor successfully verified + processed a Bridge subscription event. Dedup'd 1-per-(tenant_id, event_id) per 24-hour window (idempotency boundary).</summary>
    public static readonly AuditEventType BridgeSubscriptionEventReceived = new("BridgeSubscriptionEventReceived");

    /// <summary>Anchor rejected an event because the HMAC signature didn't verify. Dedup'd 1-per-(tenant_id, source_ip) per 1-hour window (security-relevant; flood guard).</summary>
    public static readonly AuditEventType BridgeSubscriptionEventSignatureFailed = new("BridgeSubscriptionEventSignatureFailed");

    /// <summary>Anchor rejected an event whose <c>effectiveAt</c> fell outside the ±5-minute clock-skew window per A1.2. Dedup'd 1-per-(tenant_id, event_type) per 1-hour window.</summary>
    public static readonly AuditEventType BridgeSubscriptionEventStale = new("BridgeSubscriptionEventStale");

    /// <inheritdoc />
    public override string ToString() => Value;
}
