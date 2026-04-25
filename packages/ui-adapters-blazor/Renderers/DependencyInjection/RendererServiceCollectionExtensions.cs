using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Localization;
using Sunfish.UIAdapters.Blazor.Async;
using Sunfish.UICore.Contracts;

namespace Sunfish.UIAdapters.Blazor.Renderers.DependencyInjection;

/// <summary>
/// Dependency-injection extensions that register the default Blazor renderer and
/// the in-memory client task / subscription dispatcher.
/// </summary>
public static class RendererServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BlazorDomRenderer"/> as the default
    /// <see cref="ISunfishRenderer"/>. Consumers swap this at DI time for native
    /// renderers (MAUI / Avalonia / Uno) once they exist.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSunfishBlazorDomRenderer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISunfishRenderer, BlazorDomRenderer>();
        // Wave-2 cluster D1 (Plan 2 Task 3.5) — register the framework-agnostic
        // ISunfishLocalizer<T> -> SunfishLocalizer<T> open generic so Blazor adapter
        // consumers resolve localized strings against any SharedResource marker
        // (Foundation, UICore, Blazor adapter, blocks, accelerators) without each
        // consumer having to wire it themselves. TryAdd* keeps this idempotent and
        // lets the consumer override with a custom localizer if needed.
        // NOTE: services.AddLocalization() is intentionally NOT called here — that
        // lives in consumer composition roots (apps / accelerators) per the
        // Cluster A sentinel ratification.
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
        return services;
    }

    /// <summary>
    /// Registers the in-memory <see cref="InMemoryClientTaskDispatcher"/> for
    /// bridging <see cref="IClientTask{TMessage}"/> and
    /// <see cref="IClientSubscription{TMessage}"/> to Blazor's
    /// <c>EventCallback</c> pump.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSunfishClientDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InMemoryClientTaskDispatcher>();
        return services;
    }

    /// <summary>
    /// Convenience helper that registers both the default Blazor DOM renderer and
    /// the in-memory client task / subscription dispatcher. Equivalent to calling
    /// <see cref="AddSunfishBlazorDomRenderer"/> and
    /// <see cref="AddSunfishClientDispatcher"/> in sequence.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSunfishBlazorAsync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSunfishBlazorDomRenderer();
        services.AddSunfishClientDispatcher();
        return services;
    }
}
