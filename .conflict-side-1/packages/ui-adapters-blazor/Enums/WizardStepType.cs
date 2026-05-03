namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Controls how <c>SunfishWizard</c> renders each step indicator in the stepper rail.
/// Mirrors the Telerik <c>StepperStepType</c> surface so spec samples port without edits.
/// </summary>
public enum WizardStepType
{
    /// <summary>Render the label (and, if present, icon) only — no step-number chip.</summary>
    Text,

    /// <summary>Render the numbered/indexed chip only — no label.</summary>
    Steps,

    /// <summary>Render both the chip and the label (default).</summary>
    Full,
}

/// <summary>
/// Stepper orientation for <c>SunfishWizard</c>. Prefer this over the older
/// <c>WizardStepperPosition</c>; Horizontal maps to Top, Vertical maps to Left.
/// </summary>
public enum WizardOrientation
{
    /// <summary>Stepper is laid out horizontally across the top of the content area.</summary>
    Horizontal,

    /// <summary>Stepper is laid out vertically to the left of the content area.</summary>
    Vertical,
}
