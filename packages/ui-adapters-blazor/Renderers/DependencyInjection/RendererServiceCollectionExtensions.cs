using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Async;
using Sunfish.UICore.Contracts;

namespace Sunfish.Components.Blazor.Renderers.DependencyInjection;

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
