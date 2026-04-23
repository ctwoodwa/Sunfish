namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Paper §5.3 extension-point contract. Registers a read-model projection
/// rebuilt from the event log. Projections are rebuildable at any time;
/// they hold no authoritative state.
/// </summary>
public interface IProjectionBuilder
{
    /// <summary>Stable identifier for this projection.</summary>
    string ProjectionId { get; }

    /// <summary>The stream ID this projection reads from (see <see cref="IStreamDefinition.StreamId"/>).</summary>
    string SourceStreamId { get; }

    /// <summary>Rebuild this projection from source stream head. Safe to call repeatedly.</summary>
    /// <param name="ct">Cancellation token observed during rebuild.</param>
    Task RebuildAsync(CancellationToken ct);
}
