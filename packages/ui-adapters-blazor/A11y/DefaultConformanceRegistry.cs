using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sunfish.UICore.Conformance;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Thread-safe in-process <see cref="IConformanceRegistry"/> implementation
/// per ADR 0077 §7. Registered as a DI singleton via
/// <see cref="BlazorA11yServiceExtensions.AddSunfishA11y"/>. Idempotent
/// on (LocationId, SurfaceId) — re-registering overwrites the prior entry.
/// </summary>
public sealed class DefaultConformanceRegistry : IConformanceRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConformanceDeclaration>>
        _store = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(ConformanceDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        var byLocation = _store.GetOrAdd(
            declaration.LocationId,
            _ => new ConcurrentDictionary<string, ConformanceDeclaration>(StringComparer.OrdinalIgnoreCase));
        byLocation[declaration.SurfaceId] = declaration;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConformanceDeclaration> ForLocation(string locationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        return _store.TryGetValue(locationId, out var byLocation)
            ? byLocation.Values.ToArray()
            : Array.Empty<ConformanceDeclaration>();
    }
}
