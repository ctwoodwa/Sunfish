using Sunfish.Components.Blazor.Components.Feedback;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class FeedbackTests
{
    [Fact]
    public void SunfishAlert_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishAlert);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.Feedback", type.Namespace);
    }

    [Fact]
    public void SunfishCallout_TypeExists()
    {
        var type = typeof(SunfishCallout);
        Assert.Equal("Sunfish.Components.Blazor.Components.Feedback", type.Namespace);
    }
}
