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
        Assert.Equal(AtmosphereHealth.Unknown, snapshot.Atmosphere.OverallHealth); // ADR 0082-A1: stub returns Unknown until Phase 2b
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
    /// Two-layered evidence:
    /// (1) <b>AssemblyName-level (load-bearing):</b> the implementation
    ///     assembly does NOT carry a <c>Sunfish.Foundation.Recovery</c>
    ///     reference. Without a direct ProjectReference, internal-sealed
    ///     types from foundation-recovery cannot leak in transitively;
    ///     this is the actual cohort guarantee.
    /// (2) <b>Type-graph walk (defence-in-depth):</b> every member surface
    ///     reachable on the provider type — fields, ctor parameters,
    ///     method parameters, return types, generic args, and method-body
    ///     local variables — is checked against the
    ///     <c>IFieldDecryptor</c> name. Per W#54 P2 council Major: the
    ///     prior local-variables-only walk missed parameters, fields, and
    ///     return types — the realistic mistake surface. Mono.Cecil-based
    ///     IL token scanning is deferred to a follow-up analyzer.
    /// </remarks>
    [Fact]
    public void SickBayDataProvider_DoesNotReference_IFieldDecryptor()
    {
        const string ForbiddenName = "IFieldDecryptor";
        var assembly = typeof(SickBayDataProvider).Assembly;

        // (1) AssemblyName-level check.
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain("Sunfish.Foundation.Recovery", referencedAssemblies);

        // (2) Type-graph walk on the provider type itself.
        var providerType = typeof(SickBayDataProvider);
        const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        // Fields
        foreach (var field in providerType.GetFields(AllMembers))
        {
            AssertNotForbidden(field.FieldType, $"field {field.Name}", ForbiddenName);
        }

        // Constructor parameters
        foreach (var ctor in providerType.GetConstructors(AllMembers))
        {
            foreach (var p in ctor.GetParameters())
            {
                AssertNotForbidden(p.ParameterType, $"ctor parameter {p.Name}", ForbiddenName);
            }
        }

        // Methods: parameters, return types, locals
        foreach (var method in providerType.GetMethods(AllMembers))
        {
            AssertNotForbidden(method.ReturnType, $"return of {method.Name}", ForbiddenName);
            foreach (var p in method.GetParameters())
            {
                AssertNotForbidden(p.ParameterType, $"parameter {p.Name} of {method.Name}", ForbiddenName);
            }
            var body = method.GetMethodBody();
            if (body is null) continue;
            foreach (var local in body.LocalVariables)
            {
                AssertNotForbidden(local.LocalType, $"local in {method.Name}", ForbiddenName);
            }
        }
    }

    private static void AssertNotForbidden(Type type, string site, string forbiddenName)
    {
        Assert.DoesNotContain(forbiddenName, type.FullName ?? string.Empty);
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                AssertNotForbidden(arg, site + " (generic arg)", forbiddenName);
            }
        }
    }
}
