using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Blocks.Reports.DependencyInjection;

/// <summary>
/// Service-provider-level startup helpers for the reports cluster.
/// </summary>
public static class ReportSubstrateServiceProviderExtensions
{
    /// <summary>
    /// Drain every registered <see cref="ICartridgeRegistrar"/> into
    /// the <see cref="ReportCartridgeRegistry"/>. Hosts MUST call
    /// this once at startup AFTER building the service provider and
    /// BEFORE the first
    /// <see cref="IReportRunner.RunAsync{TParams,TResult}"/>
    /// invocation.
    /// </summary>
    /// <remarks>
    /// Returns the count of registrars drained for sanity-check
    /// logging at boot. Idempotent across single-process re-call
    /// only when the registry's
    /// <see cref="ReportCartridgeRegistry.Register{TParams,TResult}"/>
    /// rejects duplicates — which it does — so a second call after
    /// the first one will throw <see cref="InvalidOperationException"/>
    /// on the first registrar.
    /// </remarks>
    /// <param name="serviceProvider">The fully-built service provider.</param>
    /// <returns>The number of registrars drained.</returns>
    public static int UseBlocksReports(this IServiceProvider serviceProvider)
    {
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));

        var registry = serviceProvider.GetRequiredService<ReportCartridgeRegistry>();
        var registrars = serviceProvider.GetServices<ICartridgeRegistrar>().ToList();
        foreach (var r in registrars) r.Register(registry);
        return registrars.Count;
    }
}
