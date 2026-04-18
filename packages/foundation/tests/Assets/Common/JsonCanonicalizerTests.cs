using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Tests.Assets.Common;

public sealed class JsonCanonicalizerTests
{
    [Fact]
    public void ProducesSameBytes_ForSameLogicalJsonWithDifferentKeyOrder()
    {
        using var a = JsonDocument.Parse("""{"z":1,"a":2,"m":{"b":3,"a":4}}""");
        using var b = JsonDocument.Parse("""{"a":2,"m":{"a":4,"b":3},"z":1}""");
        var bytesA = JsonCanonicalizer.ToCanonicalBytes(a);
        var bytesB = JsonCanonicalizer.ToCanonicalBytes(b);
        Assert.Equal(bytesA, bytesB);
    }

    [Fact]
    public void ArraysPreserveOrder()
    {
        using var a = JsonDocument.Parse("""[3,1,2]""");
        using var b = JsonDocument.Parse("""[1,2,3]""");
        Assert.NotEqual(
            JsonCanonicalizer.ToCanonicalString(a),
            JsonCanonicalizer.ToCanonicalString(b));
    }

    [Fact]
    public void HandlesPrimitivesAndNulls()
    {
        using var doc = JsonDocument.Parse("""{"s":"x","n":1,"b":true,"z":null,"arr":[]}""");
        var s = JsonCanonicalizer.ToCanonicalString(doc);
        Assert.Equal("""{"arr":[],"b":true,"n":1,"s":"x","z":null}""", s);
    }
}
