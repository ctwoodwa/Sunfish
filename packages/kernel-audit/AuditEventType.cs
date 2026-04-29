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

    /// <inheritdoc />
    public override string ToString() => Value;
}
