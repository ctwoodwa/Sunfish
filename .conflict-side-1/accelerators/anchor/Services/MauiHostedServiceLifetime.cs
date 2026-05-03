using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Drives the <see cref="IHostedService"/> lifecycle in contexts where no
/// generic <see cref="IHost"/> exists to do it for us — specifically, a .NET
/// MAUI app.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <c>Host.CreateApplicationBuilder()</c> which produces an
/// <see cref="IHost"/> (whose <c>StartAsync</c> / <c>StopAsync</c> pumps every
/// registered <see cref="IHostedService"/>), <c>MauiApp.CreateBuilder()</c>
/// produces a <c>MauiApp</c> that implements only
/// <see cref="IDisposable"/> / <see cref="IAsyncDisposable"/> and exposes a
/// bare <see cref="IServiceProvider"/>. Any <see cref="IHostedService"/>
/// registered via <c>AddHostedService&lt;T&gt;()</c> on a
/// <c>MauiAppBuilder</c> is therefore wired into DI but never started by the
/// framework itself — this class plugs that gap.
/// </para>
/// <para>
/// Verified against .NET MAUI 10 preview docs: <c>MauiApp</c> implements
/// <c>IAsyncDisposable, IDisposable</c> (not <c>IHost</c>); no lifecycle
/// contract drives <c>IHostedService</c>. See MAUI 10 dependency-injection
/// guidance and <c>MauiApp</c> type reference on learn.microsoft.com.
/// </para>
/// <para>
/// Call <see cref="StartAllAsync"/> from <c>App.CreateWindow</c> (via the
/// <c>Window.Created</c> event) and <see cref="StopAllAsync"/> from
/// <c>Window.Destroying</c>. Preserves the hosted-service contract so the
/// same <c>AddHostedService&lt;AnchorBootstrapHostedService&gt;()</c>
/// registration pattern used in <c>apps/local-node-host</c> keeps working
/// here on top of MAUI.
/// </para>
/// </remarks>
public static class MauiHostedServiceLifetime
{
    /// <summary>
    /// Resolves every <see cref="IHostedService"/> registered on
    /// <paramref name="services"/> and awaits <c>StartAsync</c> on each, in
    /// registration order (the generic-host contract). A failure on any one
    /// service is logged and rethrown — Anchor treats bootstrap failure as
    /// fatal (the app cannot render meaningfully without an active team).
    /// </summary>
    /// <param name="services">The <see cref="MauiApp.Services"/> provider.</param>
    /// <param name="cancellationToken">Cancellation for the cumulative start.</param>
    public static async Task StartAllAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var hostedServices = services.GetServices<IHostedService>();
        var logger = GetLogger(services);

        foreach (var hostedService in hostedServices)
        {
            logger.LogInformation(
                "MauiHostedServiceLifetime: starting {HostedService}",
                hostedService.GetType().FullName);
            try
            {
                await hostedService.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "MauiHostedServiceLifetime: {HostedService} failed to start",
                    hostedService.GetType().FullName);
                throw;
            }
        }
    }

    /// <summary>
    /// Resolves every <see cref="IHostedService"/> registered on
    /// <paramref name="services"/> and awaits <c>StopAsync</c> on each in
    /// reverse registration order (mirrors the generic-host shutdown
    /// contract). Exceptions are logged but not rethrown — shutdown is
    /// best-effort and must not prevent process exit.
    /// </summary>
    /// <param name="services">The <see cref="MauiApp.Services"/> provider.</param>
    /// <param name="cancellationToken">Cancellation for the cumulative stop.</param>
    public static async Task StopAllAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Reverse so teardown undoes setup in LIFO order, matching the generic
        // host's behaviour.
        var hostedServices = services.GetServices<IHostedService>().Reverse().ToArray();
        var logger = GetLogger(services);

        foreach (var hostedService in hostedServices)
        {
            try
            {
                await hostedService.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Deliberately swallow — shutdown failures must not block
                // process exit on a client shell.
                logger.LogWarning(
                    ex,
                    "MauiHostedServiceLifetime: {HostedService} threw during stop (ignored)",
                    hostedService.GetType().FullName);
            }
        }
    }

    private static ILogger GetLogger(IServiceProvider services)
    {
        var factory = services.GetService<ILoggerFactory>();
        return factory is null
            ? NullLogger.Instance
            : factory.CreateLogger(typeof(MauiHostedServiceLifetime).FullName!);
    }
}
