using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Aggregate Sick Bay snapshot per ADR 0082 §1. The Sick Bay dashboard
/// renders all four tabs (Pharmacy / Lab / Atmosphere / Medevac) from a
/// single materialized snapshot.
/// </summary>
/// <param name="Pharmacy">Per-field-purpose pharmacy entries.</param>
/// <param name="Lab">Per-probe diagnostic results.</param>
/// <param name="Atmosphere">Aggregate atmospheric readout.</param>
/// <param name="MedevacState">Current Medevac state-machine value for the tenant.</param>
/// <param name="CapturedAt">Wall-clock time the snapshot was materialized.</param>
public sealed record SickBaySnapshot(
    IReadOnlyList<PharmacyInventoryEntry> Pharmacy,
    IReadOnlyList<LabDiagnosticResult> Lab,
    AtmosphereReadout Atmosphere,
    MedevacState MedevacState,
    DateTimeOffset CapturedAt);
