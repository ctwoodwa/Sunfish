using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Default <see cref="IDimensionProbe{HardwareCapabilities}"/> per ADR
/// 0062-A1.6. Reads CPU + RAM + storage signals from the .NET runtime;
/// no external dependencies.
/// </summary>
public sealed class DefaultHardwareProbe : IDimensionProbe<HardwareCapabilities>
{
    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.Hardware;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<HardwareCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ulong? ramMb = null;
        try
        {
            ramMb = (ulong)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024L * 1024L));
        }
        catch
        {
            // Some hosts (constrained sandboxes) deny memory info; fall through to null.
        }

        return ValueTask.FromResult(new HardwareCapabilities
        {
            CpuArch = RuntimeInformation.ProcessArchitecture.ToString(),
            CpuLogicalCores = Environment.ProcessorCount,
            RamTotalMb = ramMb,
            StorageAvailableMb = null,
            HasGpu = null,
            ProbeStatus = ProbeStatus.Healthy,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{RuntimeCapabilities}"/> per A1.6.
/// Reads OS family + version + .NET version from the runtime.
/// </summary>
public sealed class DefaultRuntimeProbe : IDimensionProbe<RuntimeCapabilities>
{
    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.Runtime;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<RuntimeCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new RuntimeCapabilities
        {
            ProcessArch = RuntimeInformation.ProcessArchitecture.ToString(),
            OsFamily = OsFamilyOf(),
            OsVersion = Environment.OSVersion.Version.ToString(),
            DotnetVersion = Environment.Version.ToString(),
            ProbeStatus = ProbeStatus.Healthy,
        });
    }

    private static string OsFamilyOf()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "MacOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
        return "Other";
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{NetworkCapabilities}"/> per A1.6.
/// Reads online-status from <see cref="NetworkInterface.GetIsNetworkAvailable"/>;
/// mesh + metered signals are host-provided (default null).
/// </summary>
public sealed class DefaultNetworkProbe : IDimensionProbe<NetworkCapabilities>
{
    private readonly Func<bool>? _meshDetector;
    private readonly Func<bool>? _meteredDetector;

    /// <summary>Default — uses framework-detected online status only.</summary>
    public DefaultNetworkProbe()
    {
    }

    /// <summary>Host-provided detectors for mesh + metered signals.</summary>
    public DefaultNetworkProbe(Func<bool>? meshDetector, Func<bool>? meteredDetector)
    {
        _meshDetector = meshDetector;
        _meteredDetector = meteredDetector;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.Network;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Medium;

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA1031", Justification = "Probe must not throw on platform NIC enumeration failure.")]
    public ValueTask<NetworkCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        bool isOnline;
        try
        {
            isOnline = NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            return ValueTask.FromResult(new NetworkCapabilities
            {
                IsOnline = false,
                HasMeshVpn = null,
                IsMeteredConnection = null,
                ProbeStatus = ProbeStatus.PartiallyDegraded,
            });
        }

        return ValueTask.FromResult(new NetworkCapabilities
        {
            IsOnline = isOnline,
            HasMeshVpn = SafeInvoke(_meshDetector),
            IsMeteredConnection = SafeInvoke(_meteredDetector),
            ProbeStatus = ProbeStatus.Healthy,
        });
    }

    private static bool? SafeInvoke(Func<bool>? f)
    {
        if (f is null) return null;
        try { return f(); }
        catch { return null; }
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{UserCapabilities}"/> per A1.6.
/// Host wires a delegate that returns the principal context; no signed-in
/// user yields <see cref="ProbeStatus.Healthy"/> with <see cref="UserCapabilities.IsSignedIn"/>=false.
/// </summary>
public sealed class DefaultUserProbe : IDimensionProbe<UserCapabilities>
{
    private readonly Func<CancellationToken, ValueTask<UserCapabilities>>? _source;

    /// <summary>Default — anonymous user; <see cref="UserCapabilities.IsSignedIn"/>=false.</summary>
    public DefaultUserProbe() { }

    /// <summary>Host-provided user-context source.</summary>
    public DefaultUserProbe(Func<CancellationToken, ValueTask<UserCapabilities>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.User;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<UserCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new UserCapabilities
        {
            IsSignedIn = false,
            ProbeStatus = ProbeStatus.Healthy,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{EditionCapabilities}"/> per A1.6.
/// Host wires a delegate (e.g., reads from license cache); no source wired
/// returns <see cref="ProbeStatus.Unreachable"/>.
/// </summary>
public sealed class DefaultEditionProbe : IDimensionProbe<EditionCapabilities>
{
    private readonly Func<CancellationToken, ValueTask<EditionCapabilities>>? _source;

    /// <summary>Default — no edition source wired; status=Unreachable.</summary>
    public DefaultEditionProbe() { }

    /// <summary>Host-provided edition-source delegate.</summary>
    public DefaultEditionProbe(Func<CancellationToken, ValueTask<EditionCapabilities>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.Edition;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<EditionCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new EditionCapabilities
        {
            EditionKey = null,
            IsTrial = null,
            TrialExpiresAt = null,
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{RegulatoryCapabilities}"/> per A1.6 — Phase 1 stub.
/// Substrate Phase 1 carries probe status + jurisdiction list only; rule-content
/// evaluation is W#39 (Foundation.MissionSpace.Regulatory).
/// </summary>
public sealed class DefaultRegulatoryProbe : IDimensionProbe<RegulatoryCapabilities>
{
    private readonly Func<CancellationToken, ValueTask<RegulatoryCapabilities>>? _source;

    /// <summary>Default — no jurisdiction source wired; status=Unreachable.</summary>
    public DefaultRegulatoryProbe() { }

    /// <summary>Host-provided jurisdiction source.</summary>
    public DefaultRegulatoryProbe(Func<CancellationToken, ValueTask<RegulatoryCapabilities>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.Regulatory;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Medium;

    /// <inheritdoc />
    public ValueTask<RegulatoryCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new RegulatoryCapabilities
        {
            JurisdictionCodes = Array.Empty<string>(),
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{TrustAnchorCapabilities}"/> per A1.6.
/// Host wires a delegate that reflects identity-key + trusted-peer state;
/// no source wired returns <see cref="ProbeStatus.Unreachable"/>.
/// </summary>
public sealed class DefaultTrustAnchorProbe : IDimensionProbe<TrustAnchorCapabilities>
{
    private readonly Func<CancellationToken, ValueTask<TrustAnchorCapabilities>>? _source;

    /// <summary>Default — no trust-anchor source wired; status=Unreachable.</summary>
    public DefaultTrustAnchorProbe() { }

    /// <summary>Host-provided trust-anchor source.</summary>
    public DefaultTrustAnchorProbe(Func<CancellationToken, ValueTask<TrustAnchorCapabilities>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.TrustAnchor;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<TrustAnchorCapabilities> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new TrustAnchorCapabilities
        {
            HasIdentityKey = false,
            TrustedPeerCount = null,
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{SyncStateSnapshot}"/> per A1.6.
/// Wraps the host's W#37 sync-state source; no source wired returns
/// <see cref="ProbeStatus.Unreachable"/>.
/// </summary>
public sealed class DefaultSyncStateProbe : IDimensionProbe<SyncStateSnapshot>
{
    private readonly Func<CancellationToken, ValueTask<SyncStateSnapshot>>? _source;

    /// <summary>Default — no sync-state source wired; status=Unreachable.</summary>
    public DefaultSyncStateProbe() { }

    /// <summary>Host-provided sync-state source.</summary>
    public DefaultSyncStateProbe(Func<CancellationToken, ValueTask<SyncStateSnapshot>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.SyncState;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<SyncStateSnapshot> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new SyncStateSnapshot
        {
            State = SyncState.Offline,
            LastSyncedAt = null,
            ConflictCount = null,
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{VersionVectorSnapshot}"/> per A1.6.
/// Wraps the host's W#34 version-vector source; no source wired returns
/// <see cref="ProbeStatus.Unreachable"/>.
/// </summary>
public sealed class DefaultVersionVectorProbe : IDimensionProbe<VersionVectorSnapshot>
{
    private readonly Func<CancellationToken, ValueTask<VersionVectorSnapshot>>? _source;

    /// <summary>Default — no version-vector source wired; status=Unreachable.</summary>
    public DefaultVersionVectorProbe() { }

    /// <summary>Host-provided version-vector source.</summary>
    public DefaultVersionVectorProbe(Func<CancellationToken, ValueTask<VersionVectorSnapshot>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.VersionVector;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<VersionVectorSnapshot> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new VersionVectorSnapshot
        {
            Vector = null,
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}

/// <summary>
/// Default <see cref="IDimensionProbe{FormFactorSnapshot}"/> per A1.6.
/// Wraps the host's W#35 form-factor source; no source wired returns
/// <see cref="ProbeStatus.Unreachable"/>.
/// </summary>
public sealed class DefaultFormFactorProbe : IDimensionProbe<FormFactorSnapshot>
{
    private readonly Func<CancellationToken, ValueTask<FormFactorSnapshot>>? _source;

    /// <summary>Default — no form-factor source wired; status=Unreachable.</summary>
    public DefaultFormFactorProbe() { }

    /// <summary>Host-provided form-factor source.</summary>
    public DefaultFormFactorProbe(Func<CancellationToken, ValueTask<FormFactorSnapshot>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public DimensionChangeKind Dimension => DimensionChangeKind.FormFactor;

    /// <inheritdoc />
    public ProbeCostClass CostClass => ProbeCostClass.Low;

    /// <inheritdoc />
    public ValueTask<FormFactorSnapshot> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_source is not null) return _source(ct);
        return ValueTask.FromResult(new FormFactorSnapshot
        {
            Profile = null,
            ProbeStatus = ProbeStatus.Unreachable,
        });
    }
}
