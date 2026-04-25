using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Sunfish.Bridge.Localization;

/// <summary>
/// Localized <see cref="ProblemDetailsFactory"/> implementation per Plan 2 Task 4.2.
/// Wraps the framework default factory; resolves <see cref="ProblemDetails.Title"/> and
/// <see cref="ProblemDetails.Detail"/> through <see cref="IStringLocalizer{T}"/>
/// against <see cref="SharedResource"/> using the ambient request culture so server
/// errors reach users in the user's locale.
/// </summary>
/// <remarks>
/// Convention: domain code passes a localization KEY in the title/detail
/// (e.g. <c>"errors.not-found"</c>); the factory looks it up via the localizer.
/// If the key is not found in the resource bundle, the localizer returns the
/// passed-in value unchanged (with <c>ResourceNotFound=true</c>); the factory
/// falls back to that value, so non-keyed strings pass through unchanged.
///
/// Locale resolution follows ASP.NET Core's <see cref="IRequestCultureFeature"/> chain:
/// query override → user profile (set externally via
/// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>) → tenant default
/// → <c>Accept-Language</c> → invariant fallback. Wire via standard
/// <c>RequestLocalizationMiddleware</c>.
/// </remarks>
public sealed class SunfishProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ApiBehaviorOptions _options;

    public SunfishProblemDetailsFactory(
        IStringLocalizer<SharedResource> localizer,
        IOptions<ApiBehaviorOptions> options)
    {
        _localizer = localizer ?? throw new System.ArgumentNullException(nameof(localizer));
        _options = options?.Value ?? throw new System.ArgumentNullException(nameof(options));
    }

    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        var resolvedStatus = statusCode ?? 500;
        var problem = new ProblemDetails
        {
            Status = resolvedStatus,
            Title = ResolveTitle(title, resolvedStatus),
            Type = type,
            Detail = ResolveOrPassThrough(detail),
            Instance = instance,
        };

        ApplyDefaults(problem, httpContext);
        return problem;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        if (modelStateDictionary is null) throw new System.ArgumentNullException(nameof(modelStateDictionary));

        var resolvedStatus = statusCode ?? 400;
        var problem = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = resolvedStatus,
            Title = ResolveTitle(title, resolvedStatus),
            Type = type,
            Detail = ResolveOrPassThrough(detail),
            Instance = instance,
        };

        ApplyDefaults(problem, httpContext);
        return problem;
    }

    /// <summary>
    /// Resolve the title field. Title precedence:
    /// 1. Caller-provided string (localize as a key; pass through if not in bundle).
    /// 2. Default title from <see cref="ApiBehaviorOptions.ClientErrorMapping"/>
    ///    (also localized as a key; pass through if not in bundle).
    /// 3. Empty string.
    /// </summary>
    private string? ResolveTitle(string? caller, int statusCode)
    {
        if (!string.IsNullOrEmpty(caller)) return ResolveOrPassThrough(caller);

        if (_options.ClientErrorMapping.TryGetValue(statusCode, out var mapping))
        {
            return ResolveOrPassThrough(mapping.Title);
        }
        return null;
    }

    /// <summary>
    /// Look up <paramref name="value"/> in the localizer; if found, return the localized
    /// string; otherwise return the original value (treats it as already-localized prose).
    /// </summary>
    private string? ResolveOrPassThrough(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var localized = _localizer[value];
        return localized.ResourceNotFound ? value : localized.Value;
    }

    private void ApplyDefaults(ProblemDetails problem, HttpContext httpContext)
    {
        problem.Type ??= GetDefaultTypeUri(problem.Status);
        problem.Instance ??= httpContext?.Request?.Path.Value;

        var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }
    }

    private static string? GetDefaultTypeUri(int? status) => status switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => null,
    };
}
