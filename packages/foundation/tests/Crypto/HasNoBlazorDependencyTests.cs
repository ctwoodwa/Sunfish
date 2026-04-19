using System.Reflection;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

/// <summary>
/// Guards that the Sunfish.Foundation assembly (as reached via a Crypto type) does not
/// reference Blazor / AspNetCore.Components — directly OR transitively.
///
/// The original single-level walk only checked immediate references; a dependency that
/// pulled in AspNetCore.Components one level deeper would slip through. The BFS walk in
/// this version loads every transitively reachable assembly and asserts none of them is
/// named like Blazor or AspNetCore.Components.
///
/// Assemblies that cannot be loaded at test time (e.g. reference-only metadata assemblies)
/// are recorded in <see cref="_unloadableAssemblies"/> but not treated as failures — the
/// invariant relies on the assemblies that *can* be loaded.
/// </summary>
public class HasNoBlazorDependencyTests
{
    // Assemblies that could not be loaded during BFS — captured for diagnostics.
    private readonly List<string> _unloadableAssemblies = new();

    /// <summary>
    /// Performs a transitive BFS over all assemblies reachable from Sunfish.Foundation
    /// and asserts none is named Microsoft.AspNetCore.Components or a sub-namespace thereof.
    /// </summary>
    [Fact]
    public void FoundationAssembly_HasNoTransitiveAspNetCoreComponentsDependency()
    {
        var allNames = CollectTransitiveAssemblyNames(typeof(PrincipalId).Assembly);

        var violations = allNames
            .Where(name =>
                name.Equals("Microsoft.AspNetCore.Components", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.AspNetCore.Components.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Foundation has a transitive dependency on AspNetCore.Components: [{string.Join(", ", violations)}]");
    }

    /// <summary>
    /// Performs a transitive BFS over all assemblies reachable from Sunfish.Foundation
    /// and asserts none contains "Blazor" in its name.
    /// </summary>
    [Fact]
    public void FoundationAssembly_HasNoTransitiveBlazorDependency()
    {
        var allNames = CollectTransitiveAssemblyNames(typeof(PrincipalId).Assembly);

        var violations = allNames
            .Where(name => name.Contains("Blazor", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Foundation has a transitive dependency on Blazor: [{string.Join(", ", violations)}]");
    }

    /// <summary>
    /// Collects the names of all assemblies transitively reachable from <paramref name="root"/>
    /// via <see cref="Assembly.GetReferencedAssemblies"/> using a BFS with a seen-set guard.
    /// </summary>
    private IReadOnlyList<string> CollectTransitiveAssemblyNames(Assembly root)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();
        var allNames = new List<string>();

        queue.Enqueue(root);
        seen.Add(root.GetName().Name ?? string.Empty);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var refName in current.GetReferencedAssemblies())
            {
                var name = refName.Name ?? string.Empty;
                if (!seen.Add(name))
                    continue; // already visited

                allNames.Add(name);

                Assembly? loaded = null;
                try
                {
                    loaded = Assembly.Load(refName);
                }
                catch (FileNotFoundException)
                {
                    // Reference-only or platform-specific assembly not available in this test run.
                    _unloadableAssemblies.Add(name);
                }
                catch (Exception)
                {
                    // Any other load failure — record and skip.
                    _unloadableAssemblies.Add(name);
                }

                if (loaded is not null)
                    queue.Enqueue(loaded);
            }
        }

        return allNames;
    }
}
