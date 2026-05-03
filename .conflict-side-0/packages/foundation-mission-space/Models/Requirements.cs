using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.Transport;
using Sunfish.Foundation.UI;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per ADR 0063-A1.1 — the canonical install-time / runtime spec authored
/// by a bundle. Aggregates 10 per-dimension specs + the bundle author's
/// <see cref="SpecPolicy"/> + optional <see cref="PerPlatform"/> overrides.
/// </summary>
/// <remarks>
/// <para>
/// Wire-format per A1.5 — round-trips through
/// <see cref="Sunfish.Foundation.Crypto.CanonicalJson.Serialize"/> with
/// camelCase keys. Forward-compat per A1.5 + A1.6 verification gate
/// (option ii): <see cref="UnknownFields"/> catches any properties this
/// version doesn't recognize so they survive the round-trip.
/// </para>
/// <para>
/// Per A1.7 — per-platform overrides COMPOSE with the baseline:
/// for each dimension, the platform override replaces the baseline value
/// if present; otherwise the baseline applies. This is NOT a wholesale
/// REPLACE of the spec — the merge is per-dimension.
/// </para>
/// </remarks>
public sealed record MinimumSpec
{
    [JsonPropertyName("policy")]
    [JsonConverter(typeof(JsonStringEnumConverter<SpecPolicy>))]
    public SpecPolicy Policy { get; init; } = SpecPolicy.Recommended;

    [JsonPropertyName("hardware")]
    public HardwareSpec? Hardware { get; init; }

    [JsonPropertyName("user")]
    public UserSpec? User { get; init; }

    [JsonPropertyName("regulatory")]
    public RegulatorySpec? Regulatory { get; init; }

    [JsonPropertyName("runtime")]
    public RuntimeSpec? Runtime { get; init; }

    [JsonPropertyName("formFactor")]
    public FormFactorSpec? FormFactor { get; init; }

    [JsonPropertyName("edition")]
    public EditionSpec? Edition { get; init; }

    [JsonPropertyName("network")]
    public NetworkSpec? Network { get; init; }

    [JsonPropertyName("trust")]
    public TrustSpec? Trust { get; init; }

    [JsonPropertyName("syncState")]
    public SyncStateSpec? SyncState { get; init; }

    [JsonPropertyName("versionVector")]
    public VersionVectorSpec? VersionVector { get; init; }

    /// <summary>Per A1.7 — per-platform overrides that COMPOSE with the baseline.</summary>
    [JsonPropertyName("perPlatform")]
    public IReadOnlyList<PerPlatformSpec> PerPlatform { get; init; } = Array.Empty<PerPlatformSpec>();

    /// <summary>Per A1.5 + A1.6 — forward-compat catch-all for unknown properties.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> UnknownFields { get; init; } = new Dictionary<string, JsonElement>();
}

/// <summary>Per A1.7 — per-platform override that COMPOSES with the baseline <see cref="MinimumSpec"/>.</summary>
public sealed record PerPlatformSpec
{
    /// <summary>Platform key (e.g., <c>"ios"</c>, <c>"android"</c>, <c>"windows-desktop"</c>, <c>"macos-desktop"</c>).</summary>
    [JsonPropertyName("platform")]
    public required string Platform { get; init; }

    [JsonPropertyName("hardware")]
    public HardwareSpec? Hardware { get; init; }

    [JsonPropertyName("user")]
    public UserSpec? User { get; init; }

    [JsonPropertyName("regulatory")]
    public RegulatorySpec? Regulatory { get; init; }

    [JsonPropertyName("runtime")]
    public RuntimeSpec? Runtime { get; init; }

    [JsonPropertyName("formFactor")]
    public FormFactorSpec? FormFactor { get; init; }

    [JsonPropertyName("edition")]
    public EditionSpec? Edition { get; init; }

    [JsonPropertyName("network")]
    public NetworkSpec? Network { get; init; }

    [JsonPropertyName("trust")]
    public TrustSpec? Trust { get; init; }

    [JsonPropertyName("syncState")]
    public SyncStateSpec? SyncState { get; init; }

    [JsonPropertyName("versionVector")]
    public VersionVectorSpec? VersionVector { get; init; }
}

// ===== Per-dimension specs (10) =====

/// <summary>Per A1.1 + A1.6 — Hardware dimension spec; bytes-canonical units (post-A1.6 unit alignment).</summary>
public sealed record HardwareSpec
{
    [JsonPropertyName("minMemoryBytes")]
    public long? MinMemoryBytes { get; init; }

    [JsonPropertyName("minStorageBytes")]
    public long? MinStorageBytes { get; init; }

    [JsonPropertyName("minCpuLogicalCores")]
    public int? MinCpuLogicalCores { get; init; }

    [JsonPropertyName("requiredCpuArchitectures")]
    public IReadOnlySet<string>? RequiredCpuArchitectures { get; init; }

    [JsonPropertyName("requiresGpu")]
    public bool? RequiresGpu { get; init; }
}

/// <summary>Per A1.1 — User dimension spec.</summary>
public sealed record UserSpec
{
    [JsonPropertyName("requiresSignIn")]
    public bool? RequiresSignIn { get; init; }

    [JsonPropertyName("requiredRoles")]
    public IReadOnlySet<string>? RequiredRoles { get; init; }
}

/// <summary>Per A1.1 — Regulatory dimension spec.</summary>
public sealed record RegulatorySpec
{
    [JsonPropertyName("allowedJurisdictions")]
    public IReadOnlySet<string>? AllowedJurisdictions { get; init; }

    [JsonPropertyName("prohibitedJurisdictions")]
    public IReadOnlySet<string>? ProhibitedJurisdictions { get; init; }

    [JsonPropertyName("requiredConsents")]
    public IReadOnlySet<string>? RequiredConsents { get; init; }
}

/// <summary>Per A1.1 — Runtime dimension spec.</summary>
public sealed record RuntimeSpec
{
    [JsonPropertyName("requiredOsFamilies")]
    public IReadOnlySet<string>? RequiredOsFamilies { get; init; }

    [JsonPropertyName("minOsVersion")]
    public string? MinOsVersion { get; init; }

    [JsonPropertyName("minDotnetVersion")]
    public string? MinDotnetVersion { get; init; }
}

/// <summary>Per A1.1 — FormFactor dimension spec; consumes W#35's <see cref="FormFactorKind"/>.</summary>
public sealed record FormFactorSpec
{
    [JsonPropertyName("acceptableFormFactors")]
    public IReadOnlySet<FormFactorKind>? AcceptableFormFactors { get; init; }
}

/// <summary>Per A1.1 — Edition dimension spec; <see cref="AllowedEditions"/> accepts ADR 0009 edition keys.</summary>
public sealed record EditionSpec
{
    [JsonPropertyName("allowedEditions")]
    public IReadOnlySet<string>? AllowedEditions { get; init; }

    [JsonPropertyName("trialIsAcceptable")]
    public bool? TrialIsAcceptable { get; init; }
}

/// <summary>Per A1.3 — Network dimension spec; <see cref="RequiredTransports"/> from W#30's <see cref="TransportTier"/>.</summary>
public sealed record NetworkSpec
{
    [JsonPropertyName("requiresOnline")]
    public bool? RequiresOnline { get; init; }

    [JsonPropertyName("requiredTransports")]
    public IReadOnlySet<TransportTier>? RequiredTransports { get; init; }

    [JsonPropertyName("rejectsMeteredConnection")]
    public bool? RejectsMeteredConnection { get; init; }
}

/// <summary>Per A1.1 — Trust dimension spec.</summary>
public sealed record TrustSpec
{
    [JsonPropertyName("requiresIdentityKey")]
    public bool? RequiresIdentityKey { get; init; }

    [JsonPropertyName("minTrustedPeerCount")]
    public int? MinTrustedPeerCount { get; init; }
}

/// <summary>Per A1.2 — SyncState dimension spec; consumes W#37's <see cref="SyncState"/>.</summary>
public sealed record SyncStateSpec
{
    [JsonPropertyName("acceptableStates")]
    public IReadOnlySet<SyncState>? AcceptableStates { get; init; }
}

/// <summary>Per A1.1 — VersionVector dimension spec.</summary>
public sealed record VersionVectorSpec
{
    [JsonPropertyName("minKernelVersion")]
    public string? MinKernelVersion { get; init; }

    [JsonPropertyName("minSchemaEpoch")]
    public uint? MinSchemaEpoch { get; init; }
}

// ===== Evaluation result =====

/// <summary>Per A1.4 — operator recovery action surfaced when an evaluation fails.</summary>
public sealed record OperatorRecoveryAction
{
    [JsonPropertyName("actionKey")]
    public required string ActionKey { get; init; }

    [JsonPropertyName("argumentMap")]
    public IReadOnlyDictionary<string, string>? ArgumentMap { get; init; }
}

/// <summary>Per A1.4 — per-dimension evaluation outcome.</summary>
public sealed record DimensionEvaluation
{
    [JsonPropertyName("dimension")]
    [JsonConverter(typeof(JsonStringEnumConverter<DimensionChangeKind>))]
    public required DimensionChangeKind Dimension { get; init; }

    [JsonPropertyName("policy")]
    [JsonConverter(typeof(JsonStringEnumConverter<DimensionPolicyKind>))]
    public required DimensionPolicyKind Policy { get; init; }

    [JsonPropertyName("outcome")]
    [JsonConverter(typeof(JsonStringEnumConverter<DimensionPassFail>))]
    public required DimensionPassFail Outcome { get; init; }

    [JsonPropertyName("operatorRecoveryAction")]
    public OperatorRecoveryAction? OperatorRecoveryAction { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

/// <summary>Per A1.1 — overall result from <c>IMinimumSpecResolver.Evaluate</c>.</summary>
public sealed record SystemRequirementsResult
{
    [JsonPropertyName("overall")]
    [JsonConverter(typeof(JsonStringEnumConverter<OverallVerdict>))]
    public required OverallVerdict Overall { get; init; }

    [JsonPropertyName("dimensions")]
    public IReadOnlyList<DimensionEvaluation> Dimensions { get; init; } = Array.Empty<DimensionEvaluation>();

    [JsonPropertyName("operatorRecoveryAction")]
    public OperatorRecoveryAction? OperatorRecoveryAction { get; init; }

    [JsonPropertyName("evaluatedAt")]
    public required DateTimeOffset EvaluatedAt { get; init; }
}
