using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Wayfinder.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="SchemaRegistrationAnalyzer"/>. Per W#42 Phase 3b
/// hand-off acceptance: positive (registers descriptor — no diagnostic) +
/// negative (omits descriptor — Warning fires) + multi-call (two
/// AddSunfish*() calls; one missing — Warning fires once).
/// </summary>
public class SchemaRegistrationAnalyzerTests
{
    private sealed class Verify : CSharpAnalyzerTest<SchemaRegistrationAnalyzer, DefaultVerifier>
    {
    }

    private static Task RunAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Verify
        {
            TestCode = source,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private const string CommonStubs = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection {}

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSunfishWayfinder(this IServiceCollection s) => s;
        public static IServiceCollection AddSunfishMissionSpace(this IServiceCollection s) => s;
    }
}

namespace Sunfish.Foundation.Wayfinder
{
    public sealed class AtlasSchemaDescriptor
    {
        public AtlasSchemaDescriptor(string name) {}
    }
}
";

    [Fact]
    public Task NoAddSunfishCall_NoDiagnostic()
    {
        const string source = CommonStubs + @"
class C
{
    void M(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        // No Wayfinder DI wiring; analyzer must stay silent.
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task AddSunfish_WithDescriptorRegistration_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Wayfinder;

class C
{
    void M(IServiceCollection services)
    {
        services.AddSunfishWayfinder();
        var schema = new AtlasSchemaDescriptor(""theme"");
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task AddSunfish_WithoutDescriptorRegistration_EmitsDiagnostic()
    {
        const string source = CommonStubs + @"
using Microsoft.Extensions.DependencyInjection;

class C
{
    void M(IServiceCollection services)
    {
        {|#0:services.AddSunfishWayfinder()|};
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.SchemaRegistrationMissing)
            .WithLocation(0)
            .WithArguments("TestProject", "AddSunfishWayfinder");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task MultipleAddSunfishCalls_WithoutDescriptor_EmitsOnEveryCallSite()
    {
        const string source = CommonStubs + @"
using Microsoft.Extensions.DependencyInjection;

class C
{
    void Wire1(IServiceCollection services)
    {
        {|#0:services.AddSunfishWayfinder()|};
    }
    void Wire2(IServiceCollection services)
    {
        {|#1:services.AddSunfishMissionSpace()|};
    }
}
";
        var expected1 = new DiagnosticResult(Diagnostics.SchemaRegistrationMissing)
            .WithLocation(0)
            .WithArguments("TestProject", "AddSunfishWayfinder");
        var expected2 = new DiagnosticResult(Diagnostics.SchemaRegistrationMissing)
            .WithLocation(1)
            .WithArguments("TestProject", "AddSunfishMissionSpace");
        return RunAsync(source, expected1, expected2);
    }
}
