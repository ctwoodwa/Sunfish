namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Controls how <c>SunfishStepper</c> renders each step on the rail. Mirrors the
/// Telerik/Sunfish <c>StepperStepType</c> surface so spec samples port without edits.
/// </summary>
public enum StepType
{
    /// <summary>Render the label (and, if present, icon) only — no numbered chip.</summary>
    Text,

    /// <summary>Render the numbered/indexed chip only — no label.</summary>
    Steps,

    /// <summary>Render both the chip and the label (default).</summary>
    Full,

    /// <summary>Render labels (with validation icons inline next to the label).</summary>
    Labels,
}
