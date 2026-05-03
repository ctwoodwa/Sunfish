using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for the generic
/// <c>SunfishForm&lt;TModel&gt;</c> at <c>Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm</c>
/// (distinct from the non-generic <c>SunfishForm</c> under
/// <c>Forms.Containers</c>, which has its own harness in this folder).
/// Currently skipped: generic over <c>TModel</c> + composes <c>EditForm</c> with
/// <c>DataAnnotationsValidator</c>; requires a closed-type fixture before axe
/// coverage is meaningful. Tracked for follow-up.
/// </summary>
public class SunfishFormGenericA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishForm<TModel> is generic and composes EditForm + DataAnnotationsValidator; needs a closed-type model fixture")]
    public Task SunfishFormGeneric_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
