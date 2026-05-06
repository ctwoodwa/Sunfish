using System.Collections.Generic;

namespace Sunfish.UICore.Conformance;

/// <summary>
/// Registry of <see cref="ConformanceDeclaration"/> entries per ADR
/// 0077 §7. The W#46 a11y CI gate enumerates the registry to verify
/// every <c>ShipLocation</c>-mapped surface has a declaration with
/// <see cref="Wcag22Level"/> ≥ <c>AA</c>; surfaces without a
/// declaration fail the build.
/// </summary>
public interface IConformanceRegistry
{
    /// <summary>
    /// Register a conformance declaration. Idempotent on
    /// <c>(<see cref="ConformanceDeclaration.LocationId"/>,
    /// <see cref="ConformanceDeclaration.SurfaceId"/>)</c> — re-registering
    /// the same pair overwrites the prior entry.
    /// </summary>
    void Register(ConformanceDeclaration declaration);

    /// <summary>
    /// Returns every declaration registered to <paramref name="locationId"/>.
    /// <paramref name="locationId"/> uses the canonical lowercase
    /// wire-form of a <c>ShipLocation</c> value
    /// (e.g., <c>"quarterdeck"</c>).
    /// </summary>
    IReadOnlyList<ConformanceDeclaration> ForLocation(string locationId);
}
