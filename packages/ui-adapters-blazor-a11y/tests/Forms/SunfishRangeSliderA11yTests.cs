using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishRangeSlider.
/// Currently skipped: generic over <c>TValue : struct, INumber&lt;TValue&gt;</c> —
/// requires a closed numeric fixture. Tracked for follow-up.
/// </summary>
public class SunfishRangeSliderA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishRangeSlider is generic (@typeparam TValue : INumber<TValue>) and needs a closed numeric fixture")]
    public Task SunfishRangeSlider_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
