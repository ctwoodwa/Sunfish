using System;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<KeyPair>(_ => KeyPair.Generate());
        services.AddSingleton<NativeChannelProvider>(sp =>
        {
            var keyPair = sp.GetRequiredService<KeyPair>();
            var roster = sp.GetRequiredService<ICrewRoster>();
            var selector = sp.GetRequiredService<ITransportSelector>();
            return new NativeChannelProvider(keyPair, roster, selector);
        });
        services.AddSingleton<IChannelProvider>(sp => sp.GetRequiredService<NativeChannelProvider>());
        return services;
    }
}
