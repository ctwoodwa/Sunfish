using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Navigation.Breadcrumb;

/// <summary>
/// Wave-2 cascade extension — placeholder skip for <c>SunfishBreadcrumbItem</c>.
/// SunfishBreadcrumbItem is a child component meant to be nested inside
/// SunfishBreadcrumb's ChildContent; it emits no isolated DOM (it registers itself
/// with the parent breadcrumb). Coverage lands on SunfishBreadcrumb's structural test.
/// </summary>
public class SunfishBreadcrumbItemA11yTests
{
    [Fact(Skip = "Definition-only - registers with parent SunfishBreadcrumb, no isolated DOM")]
    public Task SunfishBreadcrumbItem_HasNoIsolatedDom() => Task.CompletedTask;
}
