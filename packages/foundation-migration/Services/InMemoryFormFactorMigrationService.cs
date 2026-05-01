using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// Reference <see cref="IFormFactorMigrationService"/> per ADR 0028-A5.
/// Phase 2 ships <see cref="ComputeDerivedSurfaceAsync"/> + the
/// derive-capabilities helper that supports it. Phase 3 wires
/// <see cref="ApplyMigrationAsync"/> and the sequestration store.
/// </summary>
public sealed class InMemoryFormFactorMigrationService : IFormFactorMigrationService
{
    /// <inheritdoc />
    public ValueTask<DerivedSurface> ComputeDerivedSurfaceAsync(
        FormFactorProfile profile,
        IReadOnlySet<string> workspaceDeclaredCapabilities,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(workspaceDeclaredCapabilities);
        ct.ThrowIfCancellationRequested();

        var hostCapabilities = DeriveHostCapabilities(profile);
        var declared = new HashSet<string>(workspaceDeclaredCapabilities, StringComparer.Ordinal);
        var included = new HashSet<string>(declared, StringComparer.Ordinal);
        included.IntersectWith(hostCapabilities);
        var excluded = new HashSet<string>(declared, StringComparer.Ordinal);
        excluded.ExceptWith(hostCapabilities);

        return ValueTask.FromResult(new DerivedSurface
        {
            FormFactor = profile.FormFactor,
            WorkspaceDeclaredCapabilities = declared,
            IncludedCapabilities = included,
            ExcludedCapabilities = excluded,
        });
    }

    /// <inheritdoc />
    public ValueTask ApplyMigrationAsync(HardwareTierChangeEvent change, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "ApplyMigrationAsync ships in Phase 3 (Invariant DLF + sequestration logic per A5.4 + A8.3).");

    /// <summary>
    /// Derives the capability tag set from a <see cref="FormFactorProfile"/>.
    /// Tags are namespaced (<c>input.*</c>, <c>display.*</c>, <c>network.*</c>,
    /// <c>power.*</c>, <c>sensor.*</c>, <c>formFactor.*</c>) so workspaces can
    /// scope their declared requirements to the relevant namespace. The set
    /// is order-stable across calls (`StringComparer.Ordinal`) so canonical-
    /// JSON encoding produces signature-stable output.
    /// </summary>
    public static HashSet<string> DeriveHostCapabilities(FormFactorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var caps = new HashSet<string>(StringComparer.Ordinal)
        {
            $"formFactor.{profile.FormFactor}",
            $"display.{profile.DisplayClass}",
            $"network.{profile.NetworkPosture}",
            $"power.{profile.PowerProfile}",
            $"instanceClass.{profile.InstanceClass}",
            $"storage.budgetMb={profile.StorageBudgetMb}",
        };
        foreach (var modality in profile.InputModalities)
        {
            caps.Add($"input.{modality}");
        }
        foreach (var sensor in profile.SensorSurface)
        {
            caps.Add($"sensor.{sensor}");
        }
        return caps;
    }
}
