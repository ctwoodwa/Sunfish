using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Lifecycle state of a <see cref="StandingOrder"/>. Per ADR 0065 §1.
/// </summary>
/// <remarks>
/// State transitions per ADR 0065:
/// <list type="bullet">
/// <item><description><see cref="Issued"/> — order created; validation pipeline not yet run.</description></item>
/// <item><description><see cref="Validated"/> — all validators passed (no Block-severity issue).</description></item>
/// <item><description><see cref="Applied"/> — the Atlas projection has incorporated the order.</description></item>
/// <item><description><see cref="Rescinded"/> — a later <c>RescindAsync</c> nullified future effect (audit immutability preserved per ADR 0049).</description></item>
/// <item><description><see cref="Rejected"/> — a Block-severity validation issue rejected the order at issuance time.</description></item>
/// <item><description><see cref="Conflicted"/> — concurrent issuance on the same (Scope, Path) lost the LWW tie-break; operator must amend and re-issue.</description></item>
/// </list>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StandingOrderState
{
    /// <summary>Order created; validation pipeline not yet run.</summary>
    Issued,

    /// <summary>All validators passed (no Block-severity issue).</summary>
    Validated,

    /// <summary>The Atlas projection has incorporated the order.</summary>
    Applied,

    /// <summary>A later <c>RescindAsync</c> nullified the order's future effect (audit immutability preserved per ADR 0049).</summary>
    Rescinded,

    /// <summary>A Block-severity validation issue rejected the order at issuance time.</summary>
    Rejected,

    /// <summary>Concurrent issuance on the same (Scope, Path) lost the LWW tie-break; operator must amend and re-issue.</summary>
    Conflicted,
}
