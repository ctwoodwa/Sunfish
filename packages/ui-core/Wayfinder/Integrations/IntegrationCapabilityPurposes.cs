namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Capability-graph purpose constants for the Atlas Integration-Config
/// surface per ADR 0067 §3.14 + council finding NM-6 (constants are
/// Phase 1 deliverables, not Phase 2 — adapter validators reference
/// these from Day 1).
/// </summary>
public static class IntegrationCapabilityPurposes
{
    /// <summary>
    /// Capability-graph purpose for short-lived decrypt grants used by
    /// integration-credential validators. Adapter validators
    /// (<see cref="IIntegrationProviderValidator"/>) request a decrypt
    /// capability with this purpose + a TTL bounded by the validation
    /// session; the capability MUST NOT be persisted across calls.
    /// </summary>
    public const string IntegrationValidation = "integration-validation";
}
