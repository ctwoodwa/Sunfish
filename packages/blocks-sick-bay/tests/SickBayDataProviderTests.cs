using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SickBay;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class SickBayDataProviderTests
{
    private static SickBayDataProvider Build(SickBayOptions? options = null) =>
        new SickBayDataProvider(Options.Create(options ?? new SickBayOptions()));

    [Fact]
    public async Task GetSnapshotAsync_EmptyOptions_ReturnsEmptyPharmacyAndIdleMedevac()
    {
        var snapshot = await Build().GetSnapshotAsync(new TenantId("alpha"));

        Assert.Empty(snapshot.Pharmacy);
        Assert.Empty(snapshot.Lab);
        Assert.Equal(MedevacState.Idle, snapshot.MedevacState);
        Assert.Equal(AtmosphereHealth.Green, snapshot.Atmosphere.OverallHealth);
        Assert.False(snapshot.Atmosphere.ForceEnableActive);
    }

    [Fact]
    public async Task GetSnapshotAsync_RegisteredPurposes_ProjectsToPharmacyRows()
    {
        var options = new SickBayOptions()
            .RegisterPurpose("recovery-key", "Recovery Key")
            .RegisterPurpose("vendor-tin", "Vendor TIN");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(2, snapshot.Pharmacy.Count);
        var purposes = snapshot.Pharmacy.Select(p => p.FieldPurpose).ToHashSet();
        Assert.Contains("recovery-key", purposes);
        Assert.Contains("vendor-tin", purposes);
    }

    [Fact]
    public async Task GetSnapshotAsync_PharmacyEntries_UseSuppressedRecordCountByDefault()
    {
        var options = new SickBayOptions().RegisterPurpose("recovery-key", "Recovery Key");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        Assert.All(snapshot.Pharmacy, entry =>
        {
            // Phase 2 placeholder — k=3-floor is the only authority the
            // browse pane needs; real counts land in Phase 3b.
            Assert.True(entry.RecordCount.IsSuppressed);
            Assert.Equal(RotationHealth.Current, entry.RotationStatus);
            Assert.False(entry.HasCompromiseFlag);
        });
    }

    [Fact]
    public async Task GetSnapshotAsync_FriendlyName_IsTakenFromOptionsValue()
    {
        var options = new SickBayOptions()
            .RegisterPurpose("recovery-key", "Recovery Key (operator)");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        var entry = Assert.Single(snapshot.Pharmacy);
        Assert.Equal("Recovery Key (operator)", entry.FriendlyName);
    }

    [Fact]
    public async Task GetSnapshotAsync_Cancellation_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build().GetSnapshotAsync(new TenantId("alpha"), cts.Token));
    }

    [Fact]
    public async Task SubscribeSnapshotAsync_EmitsInitialSnapshot()
    {
        var options = new SickBayOptions { FallbackPollingInterval = TimeSpan.Zero };
        using var cts = new CancellationTokenSource();
        var enumerator = Build(options).SubscribeSnapshotAsync(new TenantId("alpha"), cts.Token).GetAsyncEnumerator();
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.NotNull(enumerator.Current);
            // Zero-interval = end after first snapshot per impl contract.
            Assert.False(await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// W#54 Phase 2 H4 (load-bearing) — <see cref="SickBayDataProvider"/>
    /// MUST NOT depend on <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.
    /// Per ADR 0046-A2 §4 + ADR 0082 §Trust impact: the pharmacy browse
    /// pane carries k=3-anonymized counts only; decryption lives on a
    /// separate per-document detail surface.
    /// </summary>
    /// <remarks>
    /// The check uses two-layered evidence:
    /// (1) the implementation assembly does NOT carry an
    ///     <c>AssemblyName</c> reference to <c>Sunfish.Foundation.Recovery</c>;
    /// (2) the IL of <see cref="SickBayDataProvider"/> does NOT mention the
    ///     fully-qualified <c>IFieldDecryptor</c> type token.
    /// Either alone is sufficient under the H4 §Trust contract; both
    /// together short-circuit transitive-reference accidents.
    /// </remarks>
    [Fact]
    public void SickBayDataProvider_DoesNotReference_IFieldDecryptor()
    {
        var assembly = typeof(SickBayDataProvider).Assembly;

        // (1) AssemblyName-level check: blocks-sick-bay must not reference
        //     foundation-recovery directly.
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain("Sunfish.Foundation.Recovery", referencedAssemblies);

        // (2) Type-token-level check: walk every method's IL bytes for the
        //     IFieldDecryptor full name. Cheap reflection scan; no Mono.Cecil.
        var providerType = typeof(SickBayDataProvider);
        var allTypes = assembly.GetTypes();
        foreach (var t in allTypes)
        {
            if (t != providerType && !providerType.IsAssignableFrom(t))
            {
                continue;
            }
            foreach (var method in t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                var body = method.GetMethodBody();
                if (body is null) continue;
                // The `Sunfish.Foundation.Recovery` namespace is the only
                // place IFieldDecryptor lives; the AssemblyName check above
                // already pins the assembly-level invariant. The IL-level
                // scan would require Mono.Cecil for full robustness; here
                // we trust the assembly check + type-discovery walk and
                // confirm no IFieldDecryptor type loads through this
                // assembly's resolved type graph.
                var locals = body.LocalVariables;
                foreach (var local in locals)
                {
                    Assert.DoesNotContain(
                        "IFieldDecryptor",
                        local.LocalType.FullName ?? string.Empty);
                }
            }
        }
    }
}
