using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class SystemRequirementsRendererTests
{
    [Fact]
    public async Task ISystemRequirementsRenderer_ContractIsImplementable()
    {
        var renderer = new FakeRenderer();
        var surface = new FakeSurface();
        var result = new SystemRequirementsResult
        {
            Overall = OverallVerdict.Pass,
            Dimensions = Array.Empty<DimensionEvaluation>(),
            EvaluatedAt = DateTimeOffset.UtcNow,
        };

        await renderer.RenderAsync(result, surface, SystemRequirementsRenderMode.PreInstallFullPage);

        Assert.True(renderer.WasCalled);
        Assert.Equal(SystemRequirementsRenderMode.PreInstallFullPage, renderer.LastMode);
        Assert.Same(surface, renderer.LastSurface);
        Assert.Equal("test-platform", surface.Platform);
    }

    [Fact]
    public void ISystemRequirementsRenderer_DependencyInjectable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemRequirementsRenderer, FakeRenderer>();
        services.AddSingleton<ISystemRequirementsSurface, FakeSurface>();

        var sp = services.BuildServiceProvider();
        var renderer = sp.GetRequiredService<ISystemRequirementsRenderer>();
        var surface = sp.GetRequiredService<ISystemRequirementsSurface>();

        Assert.NotNull(renderer);
        Assert.NotNull(surface);
        Assert.IsType<FakeRenderer>(renderer);
        Assert.IsType<FakeSurface>(surface);
    }

    [Theory]
    [InlineData(SystemRequirementsRenderMode.PreInstallFullPage)]
    [InlineData(SystemRequirementsRenderMode.PostInstallInlineExplanation)]
    [InlineData(SystemRequirementsRenderMode.PostInstallRegressionBanner)]
    public async Task RenderMode_AllValues_AcceptedByContract(SystemRequirementsRenderMode mode)
    {
        var renderer = new FakeRenderer();
        var surface = new FakeSurface();
        var result = new SystemRequirementsResult
        {
            Overall = OverallVerdict.Pass,
            Dimensions = Array.Empty<DimensionEvaluation>(),
            EvaluatedAt = DateTimeOffset.UtcNow,
        };

        await renderer.RenderAsync(result, surface, mode);

        Assert.Equal(mode, renderer.LastMode);
    }

    private sealed class FakeRenderer : ISystemRequirementsRenderer
    {
        public bool WasCalled { get; private set; }
        public SystemRequirementsRenderMode LastMode { get; private set; }
        public ISystemRequirementsSurface? LastSurface { get; private set; }

        public ValueTask RenderAsync(
            SystemRequirementsResult result,
            ISystemRequirementsSurface surface,
            SystemRequirementsRenderMode mode,
            CancellationToken ct = default)
        {
            WasCalled = true;
            LastMode = mode;
            LastSurface = surface;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSurface : ISystemRequirementsSurface
    {
        public string Platform => "test-platform";
    }
}
