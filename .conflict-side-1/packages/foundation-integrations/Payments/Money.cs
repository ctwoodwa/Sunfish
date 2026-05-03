namespace Sunfish.Foundation.Integrations.Payments;

/// <summary>
/// Currency-bound decimal amount. W#19 Phase 0 introduces this stub for the
/// W#19 Phase 5 WorkOrder schema migration; ADR 0051 Stage 06 (W#5 substrate)
/// will extend with operators (+, -, ==), banker's-rounding helpers, and
/// validation. Phase 0 only requires the type to be constructible + comparable.
/// </summary>
/// <param name="Amount">Decimal amount.</param>
/// <param name="Currency">ISO 4217 currency.</param>
public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    /// <summary>Construct a USD-denominated <see cref="Money"/>.</summary>
    public static Money Usd(decimal amount) => new(amount, CurrencyCode.USD);
}

/// <summary>
/// ISO 4217 currency code. W#19 Phase 0 stub; ADR 0051 Stage 06 adds the full
/// allow-list validation.
/// </summary>
/// <param name="Iso4217">3-letter ISO 4217 code.</param>
public readonly record struct CurrencyCode(string Iso4217)
{
    /// <summary>The US dollar.</summary>
    public static CurrencyCode USD => new("USD");
}
