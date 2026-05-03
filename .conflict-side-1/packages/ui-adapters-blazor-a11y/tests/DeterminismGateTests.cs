using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.A11y.Tests.Fixtures;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4 Task 1.3 — the bUnit determinism gate. Binary go/no-go for the bridge:
/// if <see cref="IRenderedFragment.Markup"/> is NOT byte-identical across 100 renders of
/// the same component with the same arguments, the whole bridge approach fails and we
/// invoke ADR 0034 Option B (skip Blazor a11y gate; publish debt register).
/// </summary>
/// <remarks>
/// Tests three fixture components (simple text, attribute-heavy, RenderFragment child).
/// The gate's success criterion is strict SHA-256 equality of the markup across all
/// 100 iterations per component. Any whitespace, attribute-order, or internal-id drift
/// would fail the gate.
/// </remarks>
public class DeterminismGateTests : IClassFixture<DeterminismGateTests.BunitContextFixture>
{
    private readonly BunitContextFixture _fixture;

    public DeterminismGateTests(BunitContextFixture fixture) => _fixture = fixture;

    [Fact]
    public void SimpleTextFixture_RendersDeterministically()
        => AssertDeterministic<SimpleTextFixture>(
            parameterBuilder: p => p
                .Add(c => c.Title, "Sunfish Gate Test")
                .Add(c => c.Body, "Body text with {0} placeholder-like content."));

    [Fact]
    public void AttributedFixture_RendersDeterministically()
        => AssertDeterministic<AttributedFixture>(
            parameterBuilder: p => p
                .Add(c => c.Label, "Submit")
                .Add(c => c.Enabled, true)
                .Add(c => c.Pressed, false)
                .Add(c => c.Level, 2)
                .Add(c => c.Variant, AttributedFixture.FixtureVariant.Secondary));

    [Fact]
    public void ChildContentFixture_RendersDeterministically()
        => AssertDeterministic<ChildContentFixture>(
            parameterBuilder: p => p
                .Add(c => c.Id, "fx-1")
                .Add(c => c.Heading, "Dialog Title")
                .Add(c => c.Footer, "footer note")
                .AddChildContent("<em>composed content</em> with <strong>inline</strong> children"));

    private void AssertDeterministic<TComponent>(
        System.Action<ComponentParameterCollectionBuilder<TComponent>> parameterBuilder)
        where TComponent : IComponent
    {
        const int iterations = 100;
        var hashes = new HashSet<string>(iterations);
        string? firstMarkup = null;

        for (int i = 0; i < iterations; i++)
        {
            var fragment = _fixture.Ctx.Render<TComponent>(parameterBuilder);
            var markup = fragment.Markup;
            if (firstMarkup is null) firstMarkup = markup;
            hashes.Add(Sha256(markup));
        }

        Assert.Single(hashes);
        Assert.NotNull(firstMarkup);
        Assert.NotEmpty(firstMarkup!);
    }

    private static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// One bUnit TestContext shared across the test class. Avoids per-test setup cost
    /// (each Ctx() construction allocates a full Blazor service provider) while still
    /// letting us exercise many RenderComponent calls.
    /// </summary>
    public sealed class BunitContextFixture : System.IDisposable
    {
        public BunitContext Ctx { get; } = new();

        public void Dispose() => Ctx.Dispose();
    }
}
