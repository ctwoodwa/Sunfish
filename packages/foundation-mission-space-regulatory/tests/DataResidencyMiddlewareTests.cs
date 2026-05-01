using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sunfish.Foundation.MissionSpace.Regulatory.Bridge;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class DataResidencyMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoHeaders_PassesThroughToNext()
    {
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource());
        var middleware = new DataResidencyEnforcerMiddleware(enforcer);
        var context = NewContext();
        var nextCalled = false;
        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status451UnavailableForLegalReasons, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HeadersPresent_PermittedJurisdiction_PassesThrough()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                AllowedJurisdictions = new[] { "US-UT" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var middleware = new DataResidencyEnforcerMiddleware(enforcer);
        var context = NewContext();
        context.Request.Headers[DataResidencyEnforcerMiddleware.RecordClassHeader] = "lease";
        context.Request.Headers[DataResidencyEnforcerMiddleware.JurisdictionHeader] = "US-UT";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status451UnavailableForLegalReasons, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HeadersPresent_ProhibitedJurisdiction_Returns451()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var middleware = new DataResidencyEnforcerMiddleware(enforcer);
        var context = NewContext();
        context.Request.Headers[DataResidencyEnforcerMiddleware.RecordClassHeader] = "lease";
        context.Request.Headers[DataResidencyEnforcerMiddleware.JurisdictionHeader] = "RU";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status451UnavailableForLegalReasons, context.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", context.Response.ContentType);
        Assert.Contains("prohibited", await ReadResponseBody(context));
    }

    [Fact]
    public async Task InvokeAsync_HeadersPresent_NotInAllowedList_Returns451()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                AllowedJurisdictions = new[] { "US-UT" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var middleware = new DataResidencyEnforcerMiddleware(enforcer);
        var context = NewContext();
        context.Request.Headers[DataResidencyEnforcerMiddleware.RecordClassHeader] = "lease";
        context.Request.Headers[DataResidencyEnforcerMiddleware.JurisdictionHeader] = "EU-DE";

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        Assert.Equal(StatusCodes.Status451UnavailableForLegalReasons, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OnlyOneHeader_PassesThrough()
    {
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource());
        var middleware = new DataResidencyEnforcerMiddleware(enforcer);
        var context = NewContext();
        context.Request.Headers[DataResidencyEnforcerMiddleware.RecordClassHeader] = "lease";
        // Missing jurisdiction header — passes through.

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ResolveContextAsync_HostOverride_UsedByPipeline()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var middleware = new HardCodedContextMiddleware(enforcer, "lease", "RU");
        var context = NewContext();
        // Note: NO headers set — host override resolves context anyway.

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        Assert.Equal(StatusCodes.Status451UnavailableForLegalReasons, context.Response.StatusCode);
    }

    [Fact]
    public void Constructor_NullEnforcer_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new DataResidencyEnforcerMiddleware(null!));
    }

    private static HttpContext NewContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class HardCodedContextMiddleware : DataResidencyEnforcerMiddleware
    {
        private readonly string _recordClass;
        private readonly string _jurisdictionCode;
        public HardCodedContextMiddleware(IDataResidencyEnforcer e, string r, string j)
            : base(e) { _recordClass = r; _jurisdictionCode = j; }
        public override ValueTask<DataResidencyContext?> ResolveContextAsync(HttpContext context) =>
            ValueTask.FromResult<DataResidencyContext?>(new DataResidencyContext
            {
                RecordClass = _recordClass,
                JurisdictionCode = _jurisdictionCode,
            });
    }

    private sealed class DictSource : IDataResidencyConstraintSource
    {
        private readonly Dictionary<string, DataResidencyConstraint> _by;
        public DictSource(Dictionary<string, DataResidencyConstraint> by) => _by = by;
        public DataResidencyConstraint? GetConstraint(string recordClass) =>
            _by.TryGetValue(recordClass, out var c) ? c : null;
    }
}
