using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Reports.Cartridges.TrialBalance;

namespace Sunfish.Blocks.Reports.DependencyInjection;

/// <summary>
/// DI extension for the Trial Balance cartridge. Per the W#72 hand-off
/// §"PR 2 DI" pattern + xo-ruling-T12-50Z 3A — cartridges register
/// themselves via <see cref="ICartridgeRegistrar"/> so a single
/// startup task drains all registrars into the
/// <see cref="ReportCartridgeRegistry"/>.
/// </summary>
public static class TrialBalanceServiceCollectionExtensions
{
    /// <summary>
    /// Register the Trial Balance cartridge + its registrar. Call
    /// AFTER <see cref="ReportSubstrateServiceCollectionExtensions.AddBlocksReportsSubstrate"/>
    /// and AFTER the upstream cluster registrations
    /// (<c>AddBlocksFinancialLedger</c>, <c>AddBlocksFinancialPeriods</c>).
    /// The PR 7 umbrella <c>AddBlocksReports()</c> chains these
    /// together for hosts that want the full cluster wired in one
    /// call.
    /// </summary>
    public static IServiceCollection AddTrialBalanceCartridge(this IServiceCollection services)
    {
        if (services is null) throw new System.ArgumentNullException(nameof(services));

        services.AddSingleton<TrialBalanceCartridge>();

        // Bind the cartridge into the IReportCartridge<TParams, TResult> slot so
        // direct-resolution consumers can pick it up.
        services.AddSingleton<IReportCartridge<TrialBalanceParameters, TrialBalanceResult>>(
            sp => sp.GetRequiredService<TrialBalanceCartridge>());

        // Register the registrar so a single startup task drains all cartridges
        // into the registry via Enumerable<ICartridgeRegistrar>.
        services.AddSingleton<ICartridgeRegistrar>(sp =>
            new CartridgeRegistrar<TrialBalanceParameters, TrialBalanceResult>(
                sp.GetRequiredService<TrialBalanceCartridge>()));

        return services;
    }
}
