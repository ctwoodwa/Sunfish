namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the severity level of an alert message.
/// </summary>
public enum AlertSeverity
{
    /// <summary>Informational message with no urgency.</summary>
    Info,

    /// <summary>Positive confirmation that an action succeeded.</summary>
    Success,

    /// <summary>Cautionary message that requires attention.</summary>
    Warning,

    /// <summary>Critical error or failure that requires immediate action.</summary>
    Critical
}
