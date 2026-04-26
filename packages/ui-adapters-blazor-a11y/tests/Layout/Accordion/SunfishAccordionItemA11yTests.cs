using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.Accordion;

/// <summary>
/// SunfishAccordionItem registers itself with parent SunfishAccordion via a
/// CascadingParameter; rendering it standalone has no isolated DOM.
/// </summary>
public class SunfishAccordionItemA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishAccordion, no isolated DOM")]
    public Task SunfishAccordionItem_HasNoIsolatedDom() => Task.CompletedTask;
}
