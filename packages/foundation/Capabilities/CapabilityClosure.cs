using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Pure functions that compute the transitive closure of the capability graph over its
/// current principal set and op log. Stubbed in Task 3; Task 4 fills in the actual
/// traversal (delegation chains, group membership expansion, expiration/revocation logic).
/// </summary>
internal static class CapabilityClosure
{
    /// <summary>
    /// Decides whether <paramref name="subject"/> holds <paramref name="action"/> on
    /// <paramref name="resource"/> as of <paramref name="asOf"/>. Task 4 implementation;
    /// the Task 3 stub returns <c>false</c>.
    /// </summary>
    public static bool HasCapability(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf) => false;

    /// <summary>
    /// Finds the minimal ordered op chain that justifies the capability, or returns
    /// <c>null</c> when the subject does not hold it. Task 4 implementation;
    /// the Task 3 stub returns <c>null</c>.
    /// </summary>
    public static IReadOnlyList<SignedOperation<CapabilityOp>>? FindProofChain(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf) => null;
}
