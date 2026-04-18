using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Extensions;
using Sunfish.Foundation.Services;
using Xunit;

namespace Sunfish.Foundation.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfish_RegistersThemeService()
    {
        var services = new ServiceCollection();
        services.AddSunfish();
        var provider = services.BuildServiceProvider();

        var themeService = provider.GetService<ISunfishThemeService>();

        Assert.NotNull(themeService);
    }

    [Fact]
    public void AddSunfish_RegistersNotificationService()
    {
        var services = new ServiceCollection();
        services.AddSunfish();
        var provider = services.BuildServiceProvider();

        var notificationService = provider.GetService<ISunfishNotificationService>();

        Assert.NotNull(notificationService);
    }

    [Fact]
    public void AddSunfish_ConfigureOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSunfish(options => options.Theme = new Configuration.SunfishTheme { IsRtl = true });
        var provider = services.BuildServiceProvider();

        var themeService = provider.GetService<ISunfishThemeService>();

        Assert.NotNull(themeService);
        Assert.True(themeService!.IsRtl);
    }
}
