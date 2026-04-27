namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// Abstraction over the wall-clock used by the
/// <see cref="IRecoveryCoordinator"/> to evaluate the
/// sub-pattern <b>#48e</b> grace period. Production wires
/// <see cref="SystemRecoveryClock"/>; tests inject a controllable
/// fake so the 7-day window can be advanced synchronously.
/// </summary>
public interface IRecoveryClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow();
}
