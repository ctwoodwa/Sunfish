using System;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// One probe diagnostic result for the Sick Bay Lab tab per ADR 0082 §1.
/// Composes ADR 0062's <see cref="ProbeStatus"/> + <see cref="DegradationKind"/>
/// — the Lab tab is a tenant-friendly projection over the Mission
/// Envelope probe results.
/// </summary>
/// <param name="ProbeName">Display name for the probe (e.g., <c>"Network connectivity"</c>).</param>
/// <param name="DimensionId">Kebab-case dimension identifier (e.g., <c>"network-connectivity"</c>).</param>
/// <param name="Status">Probe status discriminator from ADR 0062.</param>
/// <param name="Degradation">Degradation kind from ADR 0062 when <see cref="Status"/> is non-Healthy.</param>
/// <param name="LastRunAt">Wall-clock time the probe last produced a result.</param>
/// <param name="DiagnosticDetail">Plain-text diagnostic detail; null when no detail is available.</param>
public sealed record LabDiagnosticResult(
    string ProbeName,
    string DimensionId,
    ProbeStatus Status,
    DegradationKind Degradation,
    DateTimeOffset LastRunAt,
    string? DiagnosticDetail);
