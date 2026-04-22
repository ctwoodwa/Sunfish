namespace Sunfish.UIAdapters.Blazor.Components.Layout;

/// <summary>
/// Event arguments for <see cref="SunfishStepper"/> step-change events. Supports
/// cancellation via <see cref="IsCancelled"/>, and exposes both the previous and
/// target step indices so handlers can gate transitions on validation state.
/// </summary>
public class StepperStepChangeEventArgs
{
    /// <summary>Zero-based index of the step the stepper is leaving.</summary>
    public int PreviousStep { get; set; }

    /// <summary>Zero-based index of the step the stepper is moving to.</summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Spec-compatible alias for <see cref="CurrentStep"/> — the index of the
    /// targeted new step. Setting either keeps both in sync at construction time.
    /// </summary>
    public int TargetIndex
    {
        get => CurrentStep;
        set => CurrentStep = value;
    }

    /// <summary>Set to <c>true</c> to cancel the step change.</summary>
    public bool IsCancelled { get; set; }
}
