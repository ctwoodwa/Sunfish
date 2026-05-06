using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// DI registration for the Helm widget substrate per ADR 0066 §1.1 +
/// cohort <c>AddSunfishX()</c> convention.
/// </summary>
public static class HelmServiceCollectionExtensions
{
    /// <summary>
    /// Register the Helm substrate with default
    /// <see cref="HelmOptions"/>. Hosts add concrete widgets via
    /// <see cref="AddHelmWidget{TWidget}"/>.
    /// </summary>
    public static IServiceCollection AddSunfishHelm(this IServiceCollection services)
        => services.AddSunfishHelm(_ => { });

    /// <summary>
    /// Register the Helm substrate with the supplied
    /// <see cref="HelmOptions"/> configuration.
    /// </summary>
    public static IServiceCollection AddSunfishHelm(
        this IServiceCollection services,
        Action<HelmOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        services.TryAddSingleton<IHelmWidgetRegistry>(sp =>
            new DefaultHelmWidgetRegistry(sp.GetServices<IHelmWidget>()));
        return services;
    }

    /// <summary>
    /// Register a concrete <see cref="IHelmWidget"/> implementation.
    /// Multiple widgets may share a slot; the
    /// <see cref="DefaultHelmWidgetRegistry"/> sorts by
    /// <see cref="HelmWidgetMetadata.OrderHint"/>.
    /// </summary>
    public static IServiceCollection AddHelmWidget<TWidget>(
        this IServiceCollection services)
        where TWidget : class, IHelmWidget
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IHelmWidget, TWidget>();
    }
}
