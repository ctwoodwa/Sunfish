using System.Linq;
using System.Threading.Tasks;
using Sunfish.Foundation.SickBay;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class DefaultFirstAidSurfaceTests
{
    [Fact]
    public async Task GetContextualHintsAsync_ReturnsEmptyForUnknownKey()
    {
        var surface = new DefaultFirstAidSurface();
        var hints = await surface.GetContextualHintsAsync("does-not-exist");
        Assert.Empty(hints);
    }

    [Fact]
    public async Task GetContextualHintsAsync_ReturnsEmptyForNullOrEmptyKey()
    {
        var surface = new DefaultFirstAidSurface();
        Assert.Empty(await surface.GetContextualHintsAsync(""));
        Assert.Empty(await surface.GetContextualHintsAsync(null!));
    }

    [Theory]
    [InlineData("pharmacy")]
    [InlineData("lab")]
    [InlineData("atmosphere")]
    public async Task GetContextualHintsAsync_ReturnsAtLeastOneHintForKnownSurface(string surfaceKey)
    {
        var surface = new DefaultFirstAidSurface();
        var hints = await surface.GetContextualHintsAsync(surfaceKey);
        Assert.NotEmpty(hints);
    }

    /// <summary>
    /// Pins the §Trust invariant on the static hint library: every
    /// hint MUST pass <see cref="FirstAidHint"/>'s plain-text validation
    /// at construction. The library can't ship an entry that smuggles
    /// HTML metacharacters past the constructor.
    /// </summary>
    [Fact]
    public async Task AllBuiltInHints_PassFirstAidHintConstructorValidation()
    {
        var surface = new DefaultFirstAidSurface();
        foreach (var key in new[] { "pharmacy", "lab", "atmosphere" })
        {
            var hints = await surface.GetContextualHintsAsync(key);
            Assert.NotEmpty(hints);
            foreach (var hint in hints)
            {
                Assert.False(string.IsNullOrEmpty(hint.Key));
                Assert.False(string.IsNullOrEmpty(hint.Title));
                Assert.False(string.IsNullOrEmpty(hint.Body));
                Assert.DoesNotContain('<', hint.Body);
                Assert.DoesNotContain('>', hint.Body);
                Assert.DoesNotContain('&', hint.Body);
            }
        }
    }

    [Fact]
    public async Task PharmacyHints_IncludeKAnonymityFloorExplainer()
    {
        var surface = new DefaultFirstAidSurface();
        var hints = await surface.GetContextualHintsAsync("pharmacy");
        Assert.Contains(hints, h => h.Key == "sick-bay.pharmacy.k-anonymity");
    }
}
