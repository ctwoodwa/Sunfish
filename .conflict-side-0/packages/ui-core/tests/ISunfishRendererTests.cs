using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

public class ISunfishRendererTests
{
    [Fact]
    public void SunfishWidgetDescriptor_RecordEquality_ByContent()
    {
        var parameters = new Dictionary<string, object?> { ["class"] = "primary" };
        var children = new List<SunfishWidgetDescriptor>();

        var a = new SunfishWidgetDescriptor("button", parameters, children);
        var b = new SunfishWidgetDescriptor("button", parameters, children);

        // Records compare by reference for reference-type properties, but since we pass
        // the *same* instances, the two descriptors should be equal.
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ISunfishRenderer_CustomImplementation_ReturnsExpectedPayload()
    {
        var sentinel = new object();
        var stub = new StubRenderer(sentinel);

        var descriptor = new SunfishWidgetDescriptor(
            "div",
            new Dictionary<string, object?>(),
            Array.Empty<SunfishWidgetDescriptor>());

        var payload = stub.Render(descriptor);

        Assert.Same(sentinel, payload);
        Assert.Equal("stub", stub.Platform);
    }

    [Fact]
    public void SunfishWidgetDescriptor_WithChildren_RoundTripsStructure()
    {
        var leaf = new SunfishWidgetDescriptor(
            "span",
            new Dictionary<string, object?> { ["id"] = "leaf-1" },
            Array.Empty<SunfishWidgetDescriptor>());
        var root = new SunfishWidgetDescriptor(
            "div",
            new Dictionary<string, object?> { ["role"] = "group" },
            new[] { leaf });

        Assert.Equal("div", root.WidgetKind);
        Assert.Single(root.Children);
        Assert.Equal("span", root.Children[0].WidgetKind);
        Assert.Equal("leaf-1", root.Children[0].Parameters["id"]);
        Assert.Equal("group", root.Parameters["role"]);
    }

    private sealed class StubRenderer(object payload) : ISunfishRenderer
    {
        public string Platform => "stub";

        public object Render(SunfishWidgetDescriptor descriptor) => payload;
    }
}
