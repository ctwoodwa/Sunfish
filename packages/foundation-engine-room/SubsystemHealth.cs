namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Per-subsystem health snapshot per ADR 0079 §1. The
/// <see cref="EngineRoomHealthSummary"/> aggregates one of these per
/// <see cref="EngineRoomSubsystem"/> value.
/// </summary>
/// <param name="Subsystem">Which subsystem this entry describes.</param>
/// <param name="Status">Discriminator for the subsystem's state.</param>
/// <param name="Message">Human-readable status detail; null for routine
/// <see cref="SubsystemStatus.Operational"/> entries; non-null for
/// <see cref="SubsystemStatus.Warning"/> / <see cref="SubsystemStatus.Critical"/>
/// per the §Trust "no blank denial" cohort norm.</param>
public sealed record SubsystemHealth(
    EngineRoomSubsystem Subsystem,
    SubsystemStatus Status,
    string? Message);
