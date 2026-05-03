namespace Sunfish.Foundation.BusinessLogic.Rules;

/// <summary>
/// An individual validation failure produced by a <see cref="IBusinessRule"/>.
/// </summary>
/// <param name="PropertyName">The property the rule targets, or <c>null</c> for object-level rules.</param>
/// <param name="Message">The human-readable description of the failure.</param>
public sealed record BrokenRule(string? PropertyName, string Message);
