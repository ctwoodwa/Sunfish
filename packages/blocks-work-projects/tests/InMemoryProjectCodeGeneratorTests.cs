using System.Text.RegularExpressions;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="InMemoryProjectCodeGenerator"/>.
/// </summary>
public sealed class InMemoryProjectCodeGeneratorTests
{
    private static readonly TenantId Tenant = new("code-test");

    [Fact]
    public async Task Next_TwoCallsSameTenantYear_ReturnsSequentialNumbers()
    {
        var sut = new InMemoryProjectCodeGenerator();
        var a = await sut.NextAsync(Tenant, 2026);
        var b = await sut.NextAsync(Tenant, 2026);

        Assert.NotEqual(a, b);
        Assert.EndsWith("L00001", a);
        Assert.EndsWith("L00002", b);
    }

    [Fact]
    public async Task Next_FormatMatchesPRJ_YYYY_RR_NNNN_pattern()
    {
        var sut = new InMemoryProjectCodeGenerator();
        var code = await sut.NextAsync(Tenant, 2026);

        Assert.Matches(@"^PRJ-2026-[A-Z][0-9][0-9]{4}$", code);
    }

    [Fact]
    public async Task Next_DifferentYears_HaveSeparateCounters()
    {
        var sut = new InMemoryProjectCodeGenerator();
        var a = await sut.NextAsync(Tenant, 2026);
        var b = await sut.NextAsync(Tenant, 2027);

        Assert.EndsWith("L00001", a);
        Assert.EndsWith("L00001", b);
        Assert.Contains("2026", a);
        Assert.Contains("2027", b);
    }

    [Fact]
    public async Task Next_YearOutOfRange_Throws()
    {
        var sut = new InMemoryProjectCodeGenerator();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.NextAsync(Tenant, 1900));
    }
}
