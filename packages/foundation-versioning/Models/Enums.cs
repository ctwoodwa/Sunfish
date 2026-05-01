namespace Sunfish.Foundation.Versioning;

/// <summary>Release-channel discriminator per ADR 0028-A6.2 rule 5 / A7.9 reword.</summary>
public enum ChannelKind
{
    Stable,
    Beta,
    Nightly,
}

/// <summary>
/// Sunfish-instance class per ADR 0028-A6.2 rule 6 / A7.6.
/// Reduced from 3 to 2 values; the prior "Embedded" was stripped per A7.6.
/// </summary>
public enum InstanceClassKind
{
    SelfHost,
    ManagedBridge,
}

/// <summary>Verdict outcome of a <see cref="VersionVector"/> compatibility evaluation.</summary>
public enum VerdictKind
{
    Compatible,
    Incompatible,
}

/// <summary>
/// Discriminator naming the specific compatibility rule that produced an
/// <see cref="VerdictKind.Incompatible"/> verdict (ADR 0028-A6.2 + A7.x).
/// </summary>
public enum FailedRule
{
    /// <summary>A6.2 rule 2 (post-A7.2 honest framing): kernel SemVer minor-version window exceeded.</summary>
    KernelSemverWindow,

    /// <summary>A6.2 rule 1: schema-epoch mismatch is a hard rejection (no auto-recovery).</summary>
    SchemaEpochMismatch,

    /// <summary>A6.2 rule 3 / A7.3 augmentation: required-plugin set intersection failure (either side requires a plugin the other doesn't carry).</summary>
    RequiredPluginIntersection,

    /// <summary>A6.2 rule 4: adapter-set asymmetry — only triggers when the intersection is empty (asymmetry alone is fine).</summary>
    AdapterSetIncompatible,

    /// <summary>A6.2 rule 5 / A7.9: release-channel ordering violated (e.g., Stable client cannot pair with Nightly relay or vice-versa).</summary>
    ChannelOrdering,

    /// <summary>A6.2 rule 6: instance-class incompatible (cross-instance pairing OK by default; specific blocking pairs trigger this).</summary>
    InstanceClassIncompatible,
}
