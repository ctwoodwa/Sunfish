using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

/// <summary>
/// Guards that the Sunfish.Foundation assembly (as reached via a Crypto type) does not
/// reference Blazor / AspNetCore.Components. This is the Phase B crypto-scoped counterpart
/// to <c>FoundationDependencyGuardTests</c> in the Assets folder, and a distinct invariant
/// from the Phase 2 ui-core test. If NSec adoption ever dragged in UI framework deps
/// transitively, this test would catch it.
/// </summary>
public class HasNoBlazorDependencyTests
{
    [Fact]
    public void FoundationAssembly_DoesNotReferenceAspNetCoreComponents()
    {
        var referenced = typeof(PrincipalId).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenced, name => name.Contains("AspNetCore.Components", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FoundationAssembly_DoesNotReferenceBlazor()
    {
        var referenced = typeof(PrincipalId).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenced, name => name.Contains("Blazor", StringComparison.OrdinalIgnoreCase));
    }
}
