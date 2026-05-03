namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Verification-time context supplied by the relying party. Each field is consulted by
/// specific first-party caveat predicates (e.g. <c>time &lt;= ...</c> uses <see cref="Now"/>,
/// <c>subject == ...</c> uses <see cref="SubjectUri"/>, etc.). Unset fields cause
/// caveats that require them to fail closed.
/// </summary>
/// <param name="Now">The instant to evaluate time-based caveats against.</param>
/// <param name="SubjectUri">The authenticated subject URI for this request, if any.</param>
/// <param name="ResourceSchema">The resource schema being accessed, if any
/// (e.g. <c>sunfish.pm.inspection/apartment</c>).</param>
/// <param name="RequestedAction">The action being attempted on the resource, if any
/// (e.g. <c>read</c>, <c>write</c>).</param>
/// <param name="DeviceIp">The requesting device's IP address, if any (used for CIDR caveats).</param>
public sealed record MacaroonContext(
    DateTimeOffset Now,
    string? SubjectUri,
    string? ResourceSchema,
    string? RequestedAction,
    string? DeviceIp);
