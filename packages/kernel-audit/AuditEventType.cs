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

    /// <inheritdoc />
    public override string ToString() => Value;
}
