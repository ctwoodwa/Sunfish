using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// A single validator in the deterministic validation chain that runs at
/// issuance time. Per ADR 0065 §3.
/// </summary>
/// <remarks>
/// Validators are composed by <c>DefaultStandingOrderIssuer</c> (Phase 2) in
/// ascending <see cref="Priority"/> order; <see cref="StandingOrderValidationSeverity.Block"/>-severity
/// issues short-circuit the chain only at issuance verdict time (every
/// validator runs to surface accumulated issues). Implementations are typically
/// scoped <c>singleton</c> and registered via
/// <c>WayfinderServiceExtensions.AddStandingOrderValidator&lt;T&gt;()</c>.
/// </remarks>
public interface IStandingOrderValidator
{
    /// <summary>
    /// Slot at which this validator runs in the chain. See
    /// <see cref="StandingOrderValidatorPriority"/>.
    /// </summary>
    StandingOrderValidatorPriority Priority { get; }

    /// <summary>
    /// Validate an order against this validator's rule set.
    /// </summary>
    /// <param name="order">The order being validated.</param>
    /// <param name="context">Ambient context (tenant + issuing actor).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validation result; an <see cref="StandingOrderValidationResult.Accepted"/> = false outcome with one or more <see cref="StandingOrderValidationSeverity.Block"/> issues rejects the order at the issuance verdict.</returns>
    ValueTask<StandingOrderValidationResult> ValidateAsync(
        StandingOrder order,
        StandingOrderContext context,
        CancellationToken ct);
}
