using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Anchor.Services;

/// <summary>
/// DI extension that wires the W#63 Phase 2 recovery-host pipeline:
/// <see cref="RecoveryHostOptions"/> + <see cref="IRecoveryCompletionHandler"/>
/// + <see cref="RecoveryGracePollingService"/>.
///
/// Per XO ruling 2026-05-16 §(c). Recovery coordinator + supporting
/// substrate (IRecoveryStateStore / IRecoveryClock / IDisputerValidator)
/// register separately (see MauiProgram.cs W#63 Phase 1 block).
/// </summary>
public static class RecoveryHostExtensions
{
    /// <summary>
    /// Registers the recovery-host pipeline. Idempotent.
    /// </summary>
    public static IServiceCollection AddAnchorRecoveryHost(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
            services.Configure<RecoveryHostOptions>(configuration.GetSection(RecoveryHostOptions.SectionName));
        else
            services.AddOptions<RecoveryHostOptions>(); // defaults

        services.AddSingleton<IRecoveryCompletionHandler, AnchorRecoveryCompletionHandler>();
        // W#67 PR 4 — IEphemeralRecoveryKeyStore default. MauiProgram
        // registers the SecureStorage-backed impl BEFORE calling this
        // extension; TryAdd preserves that production binding while
        // letting tests + non-MAUI hosts (CI, dev-mode bootstrap) fall
        // back to the in-memory fake.
        services.TryAddSingleton<IEphemeralRecoveryKeyStore, InMemoryEphemeralRecoveryKeyStore>();
        services.AddHostedService<RecoveryGracePollingService>();
        return services;
    }
}
