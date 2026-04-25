using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Localization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Localization;

/// <summary>
/// Plan 2 Task 4.2 — verify SunfishProblemDetailsFactory localizes Title + Detail
/// per ambient request culture. Uses an in-memory <see cref="IStringLocalizer{T}"/>
/// to exercise the factory without RESX path-resolution complexity (the resolution
/// path is standard ASP.NET and tested elsewhere).
/// </summary>
public class SunfishProblemDetailsFactoryTests
{
    private static readonly Dictionary<string, Dictionary<string, string>> Bundles = new()
    {
        ["en-US"] = new()
        {
            ["errors.not-found"] = "Not Found",
            ["errors.not-found.detail"] = "The requested resource could not be located.",
        },
        ["ar-SA"] = new()
        {
            ["errors.not-found"] = "غير موجود",
            ["errors.not-found.detail"] = "تعذر العثور على المورد المطلوب.",
        },
        ["ja"] = new()
        {
            ["errors.not-found"] = "見つかりません",
            ["errors.not-found.detail"] = "要求されたリソースが見つかりませんでした。",
        },
    };

    private static SunfishProblemDetailsFactory CreateFactory(string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        var localizer = new InMemoryLocalizer<SharedResource>(Bundles);
        var options = Options.Create(new ApiBehaviorOptions());
        return new SunfishProblemDetailsFactory(localizer, options);
    }

    [Theory]
    [InlineData("en-US", "Not Found", "The requested resource could not be located.")]
    [InlineData("ar-SA", "غير موجود", "تعذر العثور على المورد المطلوب.")]
    [InlineData("ja", "見つかりません", "要求されたリソースが見つかりませんでした。")]
    public void CreateProblemDetails_LocalizesTitleAndDetail(string culture, string expectedTitle, string expectedDetail)
    {
        var factory = CreateFactory(culture);
        var ctx = new DefaultHttpContext();

        var problem = factory.CreateProblemDetails(
            ctx,
            statusCode: 404,
            title: "errors.not-found",
            detail: "errors.not-found.detail");

        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedDetail, problem.Detail);
        Assert.Equal(404, problem.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem.Type);
    }

    [Fact]
    public void CreateProblemDetails_PassesThroughNonKeyedTitle()
    {
        // Non-keyed strings — i.e., already-localized prose — pass through unchanged.
        var factory = CreateFactory("en-US");
        var ctx = new DefaultHttpContext();
        const string alreadyLocalizedTitle = "An unexpected error occurred.";

        var problem = factory.CreateProblemDetails(ctx, statusCode: 500, title: alreadyLocalizedTitle);

        Assert.Equal(alreadyLocalizedTitle, problem.Title);
    }

    [Fact]
    public void CreateProblemDetails_NullTitle_LooksUpFromApiBehaviorClientErrorMapping()
    {
        // Configure an ApiBehaviorOptions ClientErrorMapping so we can verify the
        // factory looks it up when the caller passes null. Without this configuration
        // (the default), Title remains null — that's by design; callers either pass
        // a key or configure ClientErrorMapping in MVC startup.
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var localizer = new InMemoryLocalizer<SharedResource>(Bundles);
        var apiBehavior = new ApiBehaviorOptions();
        apiBehavior.ClientErrorMapping[404] = new ClientErrorData
        {
            Link = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Title = "errors.not-found",
        };
        var factory = new SunfishProblemDetailsFactory(localizer, Options.Create(apiBehavior));
        var ctx = new DefaultHttpContext();

        var problem = factory.CreateProblemDetails(ctx, statusCode: 404);

        Assert.Equal("Not Found", problem.Title);
    }

    [Fact]
    public void CreateValidationProblemDetails_LocalizesTitle()
    {
        var factory = CreateFactory("ar-SA");
        var ctx = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Name is required");

        var problem = factory.CreateValidationProblemDetails(
            ctx,
            modelState,
            statusCode: 400,
            title: "errors.not-found",
            detail: "errors.not-found.detail");

        Assert.Equal("غير موجود", problem.Title);
        Assert.Equal("تعذر العثور على المورد المطلوب.", problem.Detail);
        Assert.Single(problem.Errors);
    }

    [Fact]
    public void CreateProblemDetails_PopulatesInstanceFromRequestPath()
    {
        var factory = CreateFactory("en-US");
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/widgets/123";

        var problem = factory.CreateProblemDetails(ctx, statusCode: 404, title: "errors.not-found");

        Assert.Equal("/api/widgets/123", problem.Instance);
    }

    /// <summary>
    /// Minimal IStringLocalizer{T} backed by culture → key → value bundles. Picks the
    /// active bundle by <see cref="CultureInfo.CurrentUICulture"/>; falls back to the
    /// neutral parent (e.g. <c>ar-SA</c> → <c>ar</c>); returns the key with
    /// <see cref="LocalizedString.ResourceNotFound"/>=true if unresolved.
    /// </summary>
    private sealed class InMemoryLocalizer<T> : IStringLocalizer<T>
    {
        private readonly IReadOnlyDictionary<string, Dictionary<string, string>> _bundles;

        public InMemoryLocalizer(IReadOnlyDictionary<string, Dictionary<string, string>> bundles)
        {
            _bundles = bundles;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var resolved = Resolve(name);
                return resolved is null
                    ? new LocalizedString(name, name, resourceNotFound: true)
                    : new LocalizedString(name, resolved);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var resolved = Resolve(name);
                return resolved is null
                    ? new LocalizedString(name, name, resourceNotFound: true)
                    : new LocalizedString(name, string.Format(CultureInfo.CurrentUICulture, resolved, arguments));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            System.Linq.Enumerable.Empty<LocalizedString>();

        private string? Resolve(string key)
        {
            var culture = CultureInfo.CurrentUICulture;
            while (culture is not null && !string.IsNullOrEmpty(culture.Name))
            {
                if (_bundles.TryGetValue(culture.Name, out var bundle) && bundle.TryGetValue(key, out var v))
                {
                    return v;
                }
                culture = culture.Parent;
            }
            return null;
        }
    }
}
