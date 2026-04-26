using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout;

/// <summary>
/// SunfishCarouselSlide is a child of SunfishCarousel and registers itself with the parent;
/// it has no isolated DOM. Coverage lands on SunfishCarousel's parent test.
/// </summary>
public class SunfishCarouselSlideA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishCarousel, no isolated DOM")]
    public Task SunfishCarouselSlide_HasNoIsolatedDom() => Task.CompletedTask;
}
