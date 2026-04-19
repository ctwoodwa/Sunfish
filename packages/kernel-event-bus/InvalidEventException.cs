namespace Sunfish.Kernel.Events;

/// <summary>
/// Thrown by <see cref="IEventBus.PublishAsync"/> when the incoming signed
/// envelope fails cryptographic verification or is structurally malformed.
/// Idempotent-duplicate discards do NOT throw — they return successfully
/// with no-op semantics.
/// </summary>
public sealed class InvalidEventException : Exception
{
    /// <summary>Creates a new <see cref="InvalidEventException"/>.</summary>
    public InvalidEventException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a new <see cref="InvalidEventException"/> wrapping an underlying verifier error.</summary>
    public InvalidEventException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
