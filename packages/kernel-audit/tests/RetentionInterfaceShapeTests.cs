using System.Linq;
using System.Reflection;
using Sunfish.Kernel.Audit.Retention;
using Xunit;

namespace Sunfish.Kernel.Audit.Tests;

/// <summary>
/// PR 3b.1.5 — pin the kernel-audit retention contract shape so the
/// downstream <c>Sunfish.Foundation.SecurityPolicy.Retention</c>
/// implementation (PR 3b.2) can rely on it. Interface-only PR per
/// xo-ruling-2026-05-17T12-55Z.
/// </summary>
public sealed class RetentionInterfaceShapeTests
{
    [Fact]
    public void IAuditRetentionEnforcer_ApplyAsync_HasExpectedShape()
    {
        var method = typeof(IAuditRetentionEnforcer).GetMethod("ApplyAsync");
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("tenant", parameters[0].Name);
        Assert.Equal("policy", parameters[1].Name);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(System.Threading.Tasks.Task<RetentionEnforcementResult>), method.ReturnType);
    }

    [Fact]
    public void AuditRetentionPolicy_IsImmutableRecord_WithExpectedFields()
    {
        // Sealed positional record. Field roster: MinDays, MaxDays,
        // LegalHoldOverride, EnforcementMode (4 ctor params per xo-ruling-T12-55Z).
        // The positional-record init-only nature is guaranteed by the language;
        // no need to reflect over the setter (council A.2 — drop tautological check).
        Assert.True(typeof(AuditRetentionPolicy).IsSealed);
        var ctor = typeof(AuditRetentionPolicy).GetConstructors().Single();
        var paramNames = ctor.GetParameters().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "EnforcementMode", "LegalHoldOverride", "MaxDays", "MinDays" },
            paramNames);
    }

    [Fact]
    public void RetentionEnforcementResult_IsImmutableRecord_WithExpectedFields()
    {
        Assert.True(typeof(RetentionEnforcementResult).IsSealed);
        var ctor = typeof(RetentionEnforcementResult).GetConstructors().Single();
        var paramNames = ctor.GetParameters().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "EntriesEvaluated", "EntriesPurged", "EntriesSkippedDueToHold", "PolicyMatched" },
            paramNames);
    }

    [Fact]
    public void RetentionNamespace_ExposesExactlyFourPublicTypes()
    {
        // Per council A.1: this is a type-count pin (the §GC.1 marker presence is a
        // source-code convention enforced by reviewer not by reflection — xmldoc text is
        // stripped at runtime). Four expected: interface IAuditRetentionEnforcer + records
        // AuditRetentionPolicy + RetentionEnforcementResult + enum AuditRetentionEnforcementMode.
        var types = typeof(IAuditRetentionEnforcer).Assembly.GetTypes()
            .Where(t => t.Namespace == "Sunfish.Kernel.Audit.Retention" && t.IsPublic);
        Assert.NotEmpty(types);
        Assert.Equal(4, types.Count());
    }
}
