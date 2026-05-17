using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Blocks.Reports.DependencyInjection;

/// <summary>
/// DI extension for the reports cluster substrate.
/// </summary>
public static class ReportSubstrateServiceCollectionExtensions
{
    /// <summary>
    /// Register the cartridge substrate (registry + runner + snapshot
    /// marker stub). Does NOT register any cartridges — cartridges
    /// register themselves via their own per-cartridge extensions or
    /// via the umbrella <c>AddBlocksReports()</c> shipped in PR 7.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional <see cref="ReportRunnerOptions"/> configuration.</param>
    public static IServiceCollection AddBlocksReportsSubstrate(
        this IServiceCollection services,
        Action<ReportRunnerOptions>? configure = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.AddSingleton<ReportCartridgeRegistry>();
        services.AddSingleton<ISnapshotMarkerSource, InMemorySnapshotMarkerSource>();
        services.TryAddSingleton(TimeProvider.System);

        var options = new ReportRunnerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<IReportRunner, ReportRunner>();
        return services;
    }
}
