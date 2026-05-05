using System;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Thrown when <c>GetFieldsAsync</c> evaluates a spec whose
/// <see cref="ExtensionFieldSpec.FeatureGateOffPolicy"/> is
/// <see cref="FeatureGateOffPolicy.Redact"/> to OFF, but
/// <c>ICapabilityGraph</c> denies the redact-extension-field action. Shape
/// parallels <c>Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException</c>
/// per ADR 0075 §A0.2.
/// </summary>
public sealed class ExtensionFieldRedactionDeniedException : Exception
{
    /// <summary>Creates a denied-redaction exception with the four named context fields.</summary>
    public ExtensionFieldRedactionDeniedException(
        string action, string entityTypeFullName, string fieldKey, string reason)
        : base($"Extension-field redaction denied (action='{action}', entity='{entityTypeFullName}', field='{fieldKey}'): {reason}")
    {
        Action = action;
        EntityTypeFullName = entityTypeFullName;
        FieldKey = fieldKey;
        Reason = reason;
    }

    /// <summary>The capability action that was denied.</summary>
    public string Action { get; }

    /// <summary>The full type name of the canonical entity carrying the redacted field.</summary>
    public string EntityTypeFullName { get; }

    /// <summary>The extension-field key whose redaction was denied.</summary>
    public string FieldKey { get; }

    /// <summary>The capability-graph rationale string.</summary>
    public string Reason { get; }
}
