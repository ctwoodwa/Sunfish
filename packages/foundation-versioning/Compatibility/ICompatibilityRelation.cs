namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Evaluates a pair of <see cref="VersionVector"/>s for federation
/// compatibility per ADR 0028-A6.2 + A7.3 augmentation. Per A7.1, the
/// evaluation MUST be symmetric — <c>Evaluate(v1, v2)</c> and
/// <c>Evaluate(v2, v1)</c> must produce the same verdict so the
/// two-phase commit converges.
/// </summary>
public interface ICompatibilityRelation
{
    /// <summary>
    /// Returns <see cref="VerdictKind.Compatible"/> when the two vectors
    /// can federate, or <see cref="VerdictKind.Incompatible"/> with a
    /// <see cref="FailedRule"/> + operator-grade detail naming the first
    /// rule that rejected.
    /// </summary>
    VersionVectorVerdict Evaluate(VersionVector v1, VersionVector v2);
}
