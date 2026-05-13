using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Foundation.ShipsOffice.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="ShipsOfficePermissionAnalyzer"/>. Per ADR 0083 §2 / W#55 Phase 2d:
/// positive (call-site WITH permission check — no diagnostic) and
/// negative (call-site WITHOUT permission check — Warning fires).
/// </summary>
public class ShipsOfficePermissionAnalyzerTests
{
    private sealed class Verify : CSharpAnalyzerTest<ShipsOfficePermissionAnalyzer, DefaultVerifier>
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
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.ShipsOffice
{
    public interface IShipsOfficeDataProvider
    {
        Task<object> GetSnapshotAsync(object tenantId, CancellationToken ct = default);
        Task<object> SearchAsync(object tenantId, string query, CancellationToken ct = default);
    }
}

namespace Sunfish.Foundation.Permissions
{
    public static class ShipAction
    {
        public static readonly object ViewShipsOffice = new object();
    }

    public interface IPermissionResolver
    {
        Task<bool> AuthorizeAsync(object principal, object action, CancellationToken ct = default);
    }
}
";

    [Fact]
    public Task NoDataProviderCall_NoDiagnostic()
    {
        const string source = CommonStubs + @"
class C
{
    void M()
    {
        // No data provider call; analyzer must stay silent.
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task GetSnapshotAsync_WithPermissionCheck_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Foundation.Permissions;
using System.Threading;
using System.Threading.Tasks;

class C
{
    async Task M(IShipsOfficeDataProvider provider, IPermissionResolver resolver)
    {
        await resolver.AuthorizeAsync(null, ShipAction.ViewShipsOffice);
        var snap = await provider.GetSnapshotAsync(null);
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task GetSnapshotAsync_WithoutPermissionCheck_DiagnosticFires()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.ShipsOffice;
using System.Threading.Tasks;

class C
{
    async Task M(IShipsOfficeDataProvider provider)
    {
        var snap = await {|#0:provider.GetSnapshotAsync(null)|};
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.PermissionCheckMissing)
            .WithLocation(0)
            .WithArguments("GetSnapshotAsync");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task SearchAsync_WithoutPermissionCheck_DiagnosticFires()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.ShipsOffice;
using System.Threading.Tasks;

class C
{
    async Task M(IShipsOfficeDataProvider provider)
    {
        var results = await {|#0:provider.SearchAsync(null, ""query"")|};
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.PermissionCheckMissing)
            .WithLocation(0)
            .WithArguments("SearchAsync");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task SearchAsync_WithPermissionCheck_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Foundation.Permissions;
using System.Threading.Tasks;

class C
{
    async Task M(IShipsOfficeDataProvider provider, IPermissionResolver resolver)
    {
        await resolver.AuthorizeAsync(null, ShipAction.ViewShipsOffice);
        var results = await provider.SearchAsync(null, ""query"");
    }
}
";
        return RunAsync(source);
    }
}
