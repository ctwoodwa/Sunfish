using Sunfish.Foundation.BusinessLogic.Enums;

namespace Sunfish.Foundation.BusinessLogic.Authorization;

/// <summary>
/// A single authorization rule that determines whether a given action
/// on a named property is permitted for the current principal.
/// </summary>
public interface IAuthorizationRule
{
    /// <summary>
    /// Returns the permitted <see cref="AccessMode"/> for <paramref name="propertyName"/>.
    /// Return <c>null</c> to abstain (the engine will defer to the next rule).
    /// </summary>
    AccessMode? GetAccess(string propertyName, AuthorizationAction action, object? principal);
}
