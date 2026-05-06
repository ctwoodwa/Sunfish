using System;
using System.Collections.Generic;

namespace Sunfish.UICore.Conformance;

/// <summary>
/// Per-surface conformance declaration per ADR 0077 §7. Surfaces
/// register one declaration via <see cref="IConformanceRegistry.Register"/>
/// at module init; the W#46 a11y CI gate asserts every registered
/// surface has at least one declaration with
/// <see cref="Level"/> ≥ <see cref="Wcag22Level.AA"/>.
/// </summary>
/// <remarks>
/// W#46 P3 cycle-break: <see cref="LocationId"/> is typed
/// <see cref="string"/> rather than the
/// <c>Sunfish.Foundation.Ship.Common.ShipLocation</c> enum the hand-off
/// originally cited. <c>foundation-ship-common</c> transitively
/// references <c>foundation-wayfinder → kernel-crdt → ui-core</c>
/// (existing); a <c>ui-core → foundation-ship-common</c>
/// ProjectReference would close that loop. Consumers in
/// W#35 cohort UI blocks pass the canonical lowercase wire-form of
/// <c>ShipLocation.ToString()</c> (e.g., <c>"quarterdeck"</c>); a
/// future Phase 3b amendment may relocate <c>ShipLocation</c> to
/// <c>foundation</c> and tighten this field to the typed enum.
/// </remarks>
/// <param name="LocationId">Lowercase canonical wire-form of a <c>ShipLocation</c> value (e.g., <c>"quarterdeck"</c>).</param>
/// <param name="SurfaceId">Stable surface identifier within the location (e.g., <c>"watch-banner"</c>).</param>
/// <param name="Level">WCAG 2.2 conformance level claimed by this surface.</param>
/// <param name="Covered">Success criteria this surface explicitly covers.</param>
/// <param name="Chapters">EN 301 549 chapters this surface explicitly covers.</param>
/// <param name="Exceptions">Documented WCAG / EN exceptions; null / empty when none.</param>
/// <param name="DeclaredAt">Wall-clock time the declaration was registered.</param>
public sealed record ConformanceDeclaration(
    string LocationId,
    string SurfaceId,
    Wcag22Level Level,
    IReadOnlyList<WcagSuccessCriterion> Covered,
    IReadOnlyList<En301549Chapter> Chapters,
    IReadOnlyList<ConformanceException> Exceptions,
    DateTimeOffset DeclaredAt);
