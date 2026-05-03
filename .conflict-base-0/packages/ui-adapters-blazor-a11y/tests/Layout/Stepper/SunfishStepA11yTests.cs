using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.Stepper;

/// <summary>
/// SunfishStep, SunfishStepperStep, SunfishStepperSteps, StepperStep, and
/// StepperSteps are configuration children of SunfishStepper; no isolated DOM.
/// </summary>
public class SunfishStepA11yTests
{
    [Fact(Skip = "Definition-only - configures parent SunfishStepper, no isolated DOM")]
    public Task SunfishStep_HasNoIsolatedDom() => Task.CompletedTask;
}
