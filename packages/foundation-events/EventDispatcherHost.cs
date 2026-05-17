using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Background service that drives <see cref="SqliteEventReader"/>
/// on a polling interval per
/// <c>cross-cluster-event-bus-design.md</c> §5 + §6. Each tick
/// invokes <see cref="SqliteEventReader.DrainOnceAsync"/> for the
/// configured tenant. Handler-side failures are caught + recorded
/// inside the reader; the host loop continues regardless.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single-tenant drain in v1.</b> The host takes a single
/// <see cref="TenantId"/> at construction. Multi-tenant Anchor
/// instances (rare; canonical model is one tenant per local replica)
/// will need a v2 enhancement that walks <c>ITenantCatalog</c>. The
/// drain loop tolerates a default-constructed tenant context (treats
/// as system sentinel) so the dispatcher can run during early
/// bootstrap before tenant context is available.
/// </para>
/// </remarks>
public sealed class EventDispatcherHost : BackgroundService
{
    private readonly SqliteEventReader _reader;
    private readonly TenantId _tenantId;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public EventDispatcherHost(
        SqliteEventReader reader,
        TenantId? tenantId = null,
        TimeSpan? pollInterval = null,
        int batchSize = 100)
    {
        _reader       = reader ?? throw new ArgumentNullException(nameof(reader));
        _tenantId     = tenantId ?? TenantId.System;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Must be > 0.");
        _batchSize = batchSize;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _reader.DrainOnceAsync(_tenantId, _batchSize, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — surface to the host so it exits cleanly.
                break;
            }
            catch (Exception ex)
            {
                // The drain itself failed (not a handler failure — those are
                // caught inside DrainOnceAsync + recorded to
                // event_handler_failures). Log + sleep to avoid hot-looping.
                System.Diagnostics.Debug.WriteLine(
                    $"EventDispatcherHost drain failure: {ex}");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
