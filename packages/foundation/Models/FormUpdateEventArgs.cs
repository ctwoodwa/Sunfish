namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments for form field update events.
/// </summary>
public class FormUpdateEventArgs : EventArgs
{
    /// <summary>
    /// The model object bound to the form.
    /// </summary>
    public required object Model { get; init; }

    /// <summary>
    /// The name of the field that changed.
    /// </summary>
    public required string FieldName { get; init; }
}
