namespace Sunfish.Components.Blazor.Components.Layout;

/// <summary>
/// Specifies the position of the stepper in a wizard.
/// </summary>
public enum WizardStepperPosition
{
    /// <summary>Stepper is above the content (default).</summary>
    Top,
    /// <summary>Stepper is below the content.</summary>
    Bottom,
    /// <summary>Stepper is to the left of the content.</summary>
    Left,
    /// <summary>Stepper is to the right of the content.</summary>
    Right
}

/// <summary>
/// Event arguments for wizard step change events with cancellation support.
/// </summary>
public class WizardStepChangeEventArgs
{
    /// <summary>The index of the target step.</summary>
    public int TargetIndex { get; set; }

    /// <summary>Set to true to cancel the step change.</summary>
    public bool IsCancelled { get; set; }
}
