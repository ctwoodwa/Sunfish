namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Recovery trustee policy for a tenant per ADR 0066 §Phase 3.
/// Governs the maximum number of enrolled trustees.
/// </summary>
/// <param name="MaxTrustees">Maximum trustees allowed; 0 = recovery disabled for tenant.</param>
public sealed record TrusteePolicy(int MaxTrustees);
