using Sunfish.Ingestion.Forms;
using Xunit;

namespace Sunfish.Ingestion.Forms.Tests;

public class NoBlazorReferenceInvariantTests
{
    [Fact]
    public void IngestionFormsAssembly_DoesNotReferenceAspNetCoreComponents()
    {
        // D-NO-BLAZOR-DEPENDENCY: even though blocks-forms is a transitive project
        // reference, ingestion-forms itself must import no Blazor types. Asserted via
        // the assembly's own referenced-assemblies list.
        var assembly = typeof(FormIngestionPipeline<>).Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal) == true);
    }
}
