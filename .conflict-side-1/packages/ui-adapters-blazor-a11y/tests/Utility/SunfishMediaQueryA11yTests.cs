using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Utility;

/// <summary>
/// SunfishMediaQuery is a viewport-aware ChildContent gate that emits no DOM until
/// JS reports a matching media query; isolated bUnit testing is not meaningful for
/// the structural axe surface. Skip-marked.
/// </summary>
public class SunfishMediaQueryA11yTests
{
    [Fact(Skip = "Definition-only - viewport-gated render, no isolated DOM until JS reports match")]
    public Task SunfishMediaQuery_HasNoIsolatedDom() => Task.CompletedTask;
}
