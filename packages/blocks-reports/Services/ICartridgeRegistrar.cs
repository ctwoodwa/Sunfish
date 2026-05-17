namespace Sunfish.Blocks.Reports;

/// <summary>
/// Convention every cartridge follows so a single startup task can
/// drain all registrars into the
/// <see cref="ReportCartridgeRegistry"/> at host boot time.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>xo-ruling-2026-05-17T12-50Z</c> D3 + the W#72 hand-off
/// §"PR 2 DI" pattern. Cartridges register themselves via this
/// indirection rather than calling
/// <see cref="ReportCartridgeRegistry.Register{TParams,TResult}"/>
/// directly during DI bootstrap — defers the actual
/// <c>Register</c> call until the host has fully composed the
/// service provider.
/// </para>
/// <para>
/// Hosts MAY also bypass the registrar by resolving the registry
/// and cartridge directly + calling <c>Register</c> at startup;
/// the registrar is the convention, not a requirement.
/// </para>
/// </remarks>
public interface ICartridgeRegistrar
{
    /// <summary>Register this cartridge into the supplied registry.</summary>
    void Register(ReportCartridgeRegistry registry);
}

/// <summary>
/// Generic implementation of <see cref="ICartridgeRegistrar"/>.
/// Cartridges register themselves by composing a
/// <see cref="CartridgeRegistrar{TParams,TResult}"/> in their DI
/// extension method.
/// </summary>
public sealed class CartridgeRegistrar<TParams, TResult> : ICartridgeRegistrar
    where TParams : class
    where TResult : class
{
    private readonly IReportCartridge<TParams, TResult> _cartridge;

    /// <summary>Construct a registrar wrapping the supplied cartridge.</summary>
    public CartridgeRegistrar(IReportCartridge<TParams, TResult> cartridge)
        => _cartridge = cartridge ?? throw new System.ArgumentNullException(nameof(cartridge));

    /// <inheritdoc />
    public void Register(ReportCartridgeRegistry registry)
    {
        if (registry is null) throw new System.ArgumentNullException(nameof(registry));
        registry.Register(_cartridge);
    }
}
