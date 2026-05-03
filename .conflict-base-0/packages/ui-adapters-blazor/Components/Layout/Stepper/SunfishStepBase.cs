using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Layout;

/// <summary>
/// Shared base for declarative stepper children (<c>SunfishStep</c> legacy and
/// <c>SunfishStepperStep</c> spec-canonical). Owns registration with the parent
/// stepper, the ChildContent slot, and every parameter the parent indicator loop
/// reads — subclasses can add spec-compatible aliases on top.
/// </summary>
public abstract class SunfishStepBase : SunfishComponentBase
{
    /// <summary>Parent stepper, resolved via cascading value.</summary>
    [CascadingParameter] protected SunfishStepper? ParentStepper { get; set; }

    /// <summary>Custom icon content for the step indicator (overrides number/text/checkmark).</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Marks the step as optional — an "(Optional)" hint is rendered next to the label.</summary>
    [Parameter] public bool Optional { get; set; }

    /// <summary>Disables the step; disabled steps cannot be navigated to.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Three-state validity. <c>true</c> renders a success icon, <c>false</c> an
    /// error icon, <c>null</c> leaves the indicator alone.
    /// </summary>
    [Parameter] public bool? Valid { get; set; }

    /// <summary>When <c>true</c>, force a success state regardless of <see cref="Valid"/>.</summary>
    [Parameter] public bool? Successful { get; set; }

    /// <summary>When <c>true</c>, force an error state regardless of <see cref="Valid"/>.</summary>
    [Parameter] public bool? Error { get; set; }

    /// <summary>
    /// Short text rendered inside the step chip (overrides the numeric index).
    /// Ignored when <see cref="Icon"/> is also set.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// Override the automatic step status entirely. When <c>null</c>, status is
    /// derived from position + <see cref="Valid"/>/<see cref="Error"/>.
    /// </summary>
    [Parameter] public StepStatus? StepStatus { get; set; }

    /// <summary>
    /// Fires on this step before navigation leaves it. Set
    /// <see cref="StepperStepChangeEventArgs.IsCancelled"/> to veto the transition.
    /// </summary>
    [Parameter] public EventCallback<StepperStepChangeEventArgs> OnChange { get; set; }

    /// <summary>Panel body rendered when this step is active.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Human-readable label next to the step indicator. Subclasses resolve the exact source.</summary>
    internal abstract string? EffectiveLabel { get; }

    protected override void OnInitialized()
    {
        ParentStepper?.RegisterStep(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentStepper?.UnregisterStep(this);
        base.Dispose(disposing);
    }
}
