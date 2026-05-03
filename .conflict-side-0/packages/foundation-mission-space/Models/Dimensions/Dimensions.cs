using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>Hardware dimension snapshot per A1.2.</summary>
public sealed record HardwareCapabilities
{
    [JsonPropertyName("cpuArch")]
    public string? CpuArch { get; init; }

    [JsonPropertyName("cpuLogicalCores")]
    public int? CpuLogicalCores { get; init; }

    [JsonPropertyName("ramTotalMb")]
    public ulong? RamTotalMb { get; init; }

    [JsonPropertyName("storageAvailableMb")]
    public ulong? StorageAvailableMb { get; init; }

    [JsonPropertyName("hasGpu")]
    public bool? HasGpu { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>User dimension snapshot per A1.2.</summary>
public sealed record UserCapabilities
{
    [JsonPropertyName("principalId")]
    public string? PrincipalId { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("isSignedIn")]
    public required bool IsSignedIn { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>
/// Regulatory dimension snapshot per A1.2 — consumed by ADR 0064 W#39.
/// P1 carries probe status + jurisdiction list; rule-content lives in
/// foundation-mission-space-regulatory (W#39).
/// </summary>
public sealed record RegulatoryCapabilities
{
    [JsonPropertyName("jurisdictionCodes")]
    public IReadOnlyList<string> JurisdictionCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>Runtime dimension snapshot per A1.2.</summary>
public sealed record RuntimeCapabilities
{
    [JsonPropertyName("processArch")]
    public string? ProcessArch { get; init; }

    [JsonPropertyName("osFamily")]
    public string? OsFamily { get; init; }

    [JsonPropertyName("osVersion")]
    public string? OsVersion { get; init; }

    [JsonPropertyName("dotnetVersion")]
    public string? DotnetVersion { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>Edition dimension snapshot per A1.2 + A1.6 + A1.8.</summary>
public sealed record EditionCapabilities
{
    [JsonPropertyName("editionKey")]
    public string? EditionKey { get; init; }

    [JsonPropertyName("isTrial")]
    public bool? IsTrial { get; init; }

    [JsonPropertyName("trialExpiresAt")]
    public DateTimeOffset? TrialExpiresAt { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>
/// Network dimension snapshot per A1.2. P1 ships a lightweight surface;
/// W#30 stuck PRs (P6/P7/P8 per the ledger note) will eventually wire
/// the full <c>Sunfish.Foundation.Transport</c> tier discriminator
/// here.
/// </summary>
public sealed record NetworkCapabilities
{
    [JsonPropertyName("isOnline")]
    public required bool IsOnline { get; init; }

    [JsonPropertyName("hasMeshVpn")]
    public bool? HasMeshVpn { get; init; }

    [JsonPropertyName("isMeteredConnection")]
    public bool? IsMeteredConnection { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>Trust-anchor dimension per A1.8 rename.</summary>
public sealed record TrustAnchorCapabilities
{
    [JsonPropertyName("hasIdentityKey")]
    public required bool HasIdentityKey { get; init; }

    [JsonPropertyName("trustedPeerCount")]
    public int? TrustedPeerCount { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>Sync-state snapshot per A1.2 — wraps W#37's <see cref="SyncState"/>.</summary>
public sealed record SyncStateSnapshot
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter<SyncState>))]
    public required SyncState State { get; init; }

    [JsonPropertyName("lastSyncedAt")]
    public DateTimeOffset? LastSyncedAt { get; init; }

    [JsonPropertyName("conflictCount")]
    public int? ConflictCount { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>FormFactor snapshot per A1.2 — wraps W#35's <see cref="FormFactorProfile"/>.</summary>
public sealed record FormFactorSnapshot
{
    [JsonPropertyName("profile")]
    public FormFactorProfile? Profile { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}

/// <summary>VersionVector snapshot per A1.2 — wraps W#34's <see cref="VersionVector"/>.</summary>
public sealed record VersionVectorSnapshot
{
    [JsonPropertyName("vector")]
    public VersionVector? Vector { get; init; }

    [JsonPropertyName("probeStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))]
    public required ProbeStatus ProbeStatus { get; init; }
}
