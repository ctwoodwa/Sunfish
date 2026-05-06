using System;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Aggregate atmospheric readout for the Sick Bay Atmosphere tab per
/// ADR 0082 §1. Summarizes Mission Envelope probe results into a single
/// dashboard tile with a status banner + per-severity counts.
/// </summary>
/// <param name="OverallHealth">Aggregate discriminator for the readout.</param>
/// <param name="WarningProbeCount">Number of probes currently in <see cref="Sunfish.Foundation.MissionSpace.ProbeStatus"/> warning states.</param>
/// <param name="CriticalProbeCount">Number of probes currently in critical states.</param>
/// <param name="ForceEnableActive">True when an operator force-enable override is currently active per ADR 0062-A1.</param>
/// <param name="CapturedAt">Wall-clock time the readout was materialized.</param>
public sealed record AtmosphereReadout(
    AtmosphereHealth OverallHealth,
    int WarningProbeCount,
    int CriticalProbeCount,
    bool ForceEnableActive,
    DateTimeOffset CapturedAt);
