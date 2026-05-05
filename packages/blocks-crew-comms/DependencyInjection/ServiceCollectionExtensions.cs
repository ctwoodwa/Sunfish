using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;

namespace Sunfish.Blocks.CrewComms.DependencyInjection;

/// <summary>
/// DI surface for <c>blocks-crew-comms</c>. Per ADR 0076 §DI: register
/// <see cref="NativeChannelProvider"/> as the singleton implementation of
/// <see cref="IChannelProvider"/>; generate a fresh <see cref="KeyPair"/>
/// at startup; bind the roster via the <see cref="CrewCommsBuilder"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the native crew-comms provider. The configure delegate
    /// MUST install at least one <see cref="ICrewRoster"/> implementation
    /// (e.g., <see cref="CrewCommsBuilder.AddInMemory"/>). Caller is
    /// responsible for separately registering an
    /// <see cref="ITransportSelector"/> — typically via
    /// <c>services.AddSunfishTransport()</c>.
    /// </summary>
    public static IServiceCollection AddSunfishCrewComms(
        this IServiceCollection services,
        Action<CrewCommsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CrewCommsBuilder(services);
        configure(builder);

        // Council finding #9 — Phase-1 stub: KeyPair generated once per container
        // construction. Each Anchor restart yields a NEW PeerId; any persistent
        // roster entries keyed off PeerId go stale across restarts. A
        // production-grade identity-layer phase will substitute a persistent
        // KeyPair (loaded from secure storage) before this default factory fires.
        // Until then, callers MAY pre-register a KeyPair via
        // `services.AddSingleton<KeyPair>(myPersistentKeyPair)` BEFORE calling
        // AddSunfishCrewComms, and TryAddSingleton will respect it.
        services.TryAddSingleton<KeyPair>(_ => KeyPair.Generate());

        // Council finding #12 — register concrete + interface as one shared
        // singleton. The factory delegates to the concrete registration so
        // both `IChannelProvider` and `NativeChannelProvider` resolve to the
        // same instance. Transport adapters that need direct Listener/Presence
        // access resolve `NativeChannelProvider`; channel-API consumers
        // resolve `IChannelProvider`. One object, two surfaces.
        services.AddSingleton<NativeChannelProvider>(sp =>
        {
            var keyPair = sp.GetRequiredService<KeyPair>();
            var roster = sp.GetRequiredService<ICrewRoster>();
            var selector = sp.GetRequiredService<ITransportSelector>();
            var auditTrail = sp.GetService<Sunfish.Kernel.Audit.IAuditTrail>();
            return new NativeChannelProvider(keyPair, roster, selector, auditTrail: auditTrail);
        });
        services.AddSingleton<IChannelProvider>(sp => sp.GetRequiredService<NativeChannelProvider>());
        return services;
    }
}
