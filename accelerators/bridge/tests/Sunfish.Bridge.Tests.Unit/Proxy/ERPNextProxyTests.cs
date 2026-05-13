using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Proxy;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Proxy;

public sealed class ERPNextProxyTests
{
    [Fact]
    public async Task GetProperties_ReturnsOk_WithClientData()
    {
        var expected = JsonDocument.Parse("""{"data":[{"name":"PROP-0001","property_name":"150 Lexington Ct"}]}""").RootElement.Clone();
        var client = new FakeERPNextClient(expected);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = "Royal Key Management LLC" });

        var result = await ERPNextProxy.HandleGetPropertiesAsync(client, options, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<JsonElement>>(result);
        Assert.Equal("data", ok.Value.EnumerateObject().First().Name);
    }

    [Fact]
    public async Task GetProperties_Returns503_WhenDefaultCompanyNotConfigured()
    {
        var client = new FakeERPNextClient(default);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = "" });

        var result = await ERPNextProxy.HandleGetPropertiesAsync(client, options, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
    }

    [Fact]
    public async Task GetProperties_PassesCompanyToClient()
    {
        var payload = JsonDocument.Parse("""{"data":[]}""").RootElement.Clone();
        var client = new FakeERPNextClient(payload);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = "Elbrus Holding LLC" });

        await ERPNextProxy.HandleGetPropertiesAsync(client, options, CancellationToken.None);

        Assert.Equal("Elbrus Holding LLC", client.LastCompany);
        Assert.Equal("Property", client.LastDoctype);
    }

    private sealed class FakeERPNextClient : IERPNextClient
    {
        private readonly JsonElement _result;
        public string? LastDoctype { get; private set; }
        public string? LastCompany { get; private set; }

        public FakeERPNextClient(JsonElement result) => _result = result;

        public Task<JsonElement> GetResourceListAsync(
            string doctype, string company,
            IDictionary<string, object>? extraFilters = null,
            int limit = 20, CancellationToken ct = default)
        {
            LastDoctype = doctype;
            LastCompany = company;
            return Task.FromResult(_result);
        }

        public Task<JsonElement> GetResourceAsync(
            string doctype, string name, string company, CancellationToken ct = default)
            => Task.FromResult(_result);

        public Task<JsonElement> PostAsync(
            string endpoint, object payload, string company, CancellationToken ct = default)
            => Task.FromResult(_result);
    }
}
