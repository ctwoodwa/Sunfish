using System.Text.Json;
using Sunfish.Blocks.Properties.Models;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

public class PostalAddressTests
{
    [Fact]
    public void Json_round_trip_preserves_required_and_optional_fields()
    {
        var address = new PostalAddress
        {
            Line1 = "123 Main St",
            Line2 = "Apt 4B",
            City = "Salt Lake City",
            Region = "UT",
            PostalCode = "84101",
            CountryCode = "US",
            Latitude = 40.7608,
            Longitude = -111.8910,
        };

        var json = JsonSerializer.Serialize(address);
        var roundTripped = JsonSerializer.Deserialize<PostalAddress>(json);

        Assert.Equal(address, roundTripped);
    }

    [Fact]
    public void Equality_treats_records_with_same_fields_as_equal()
    {
        var a = new PostalAddress
        {
            Line1 = "1 First Ave",
            City = "Provo",
            Region = "UT",
            PostalCode = "84601",
            CountryCode = "US",
        };
        var b = a with { };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
