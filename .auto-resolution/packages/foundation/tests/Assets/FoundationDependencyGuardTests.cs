using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tests.Assets;

/// <summary>
/// Guards that Sunfish.Foundation does not transitively pull in Blazor / JSInterop.
/// Plan D-NO-BLAZOR, Phase 2 migration invariant.
/// </summary>
public sealed class FoundationDependencyGuardTests
{
    public static IEnumerable<object[]> ForbiddenAssemblies =>
        new[]
        {
            new object[] { "Microsoft.AspNetCore.Components" },
            new object[] { "Microsoft.AspNetCore.Components.Web" },
            new object[] { "Microsoft.JSInterop" },
            new object[] { "Microsoft.JSInterop.WebAssembly" },
        };

    [Theory]
    [MemberData(nameof(ForbiddenAssemblies))]
    public void Foundation_HasNoBlazorDependency(string forbiddenAssembly)
    {
        var referenced = typeof(EntityId).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain(forbiddenAssembly, referenced);
    }
}
