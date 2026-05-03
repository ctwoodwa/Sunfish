using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Bridge.Localization;

/// <summary>
/// DI extensions for wiring the Sunfish-localized ProblemDetailsFactory into a Bridge
/// composition root per Plan 2 Task 4.2. Pair with <c>services.AddLocalization()</c>
/// and <c>app.UseRequestLocalization(...)</c> so request-culture flows through.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replace the default <see cref="ProblemDetailsFactory"/> with
    /// <see cref="SunfishProblemDetailsFactory"/>. Resolves error title + detail
    /// through <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
    /// against <see cref="SharedResource"/>.
    /// </summary>
    /// <remarks>
    /// Idempotent — repeat calls leave the registration as-is. Intended to be invoked
    /// once during app composition. Caller is responsible for adding
    /// <c>AddLocalization()</c> + the request-localization middleware.
    /// </remarks>
    public static IServiceCollection AddSunfishLocalizedProblemDetails(this IServiceCollection services)
    {
        if (services is null) throw new System.ArgumentNullException(nameof(services));
        services.Replace(ServiceDescriptor.Singleton<ProblemDetailsFactory, SunfishProblemDetailsFactory>());
        return services;
    }
}
