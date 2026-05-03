using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Versions;

/// <summary>
/// Post-commit hook fired whenever a new <see cref="Version"/> is appended.
/// </summary>
/// <remarks>
/// Plan D-EXTENSIBILITY-SEAMS. Phase A ships <see cref="NullVersionObserver"/>; Phase C will
/// wire this to the event bus so Automerge-style sync can propagate changes.
/// </remarks>
public interface IVersionObserver
{
    /// <summary>Invoked after a new version is appended and the "current body" cache updated.</summary>
    Task OnVersionAppendedAsync(EntityId entity, Version version, CancellationToken ct = default);
}

/// <summary>Null-object observer that ignores every event. Phase A default.</summary>
public sealed class NullVersionObserver : IVersionObserver
{
    /// <summary>Singleton instance.</summary>
    public static NullVersionObserver Instance { get; } = new();

    /// <inheritdoc />
    public Task OnVersionAppendedAsync(EntityId entity, Version version, CancellationToken ct = default)
        => Task.CompletedTask;
}
