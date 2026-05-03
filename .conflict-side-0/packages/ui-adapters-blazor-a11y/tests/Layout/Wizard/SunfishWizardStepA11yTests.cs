using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.Wizard;

/// <summary>
/// SunfishWizardStep, SunfishWizardSteps, and WizardStep are configuration
/// children of SunfishWizard; no isolated DOM.
/// </summary>
public class SunfishWizardStepA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishWizard, no isolated DOM")]
    public Task SunfishWizardStep_HasNoIsolatedDom() => Task.CompletedTask;
}
