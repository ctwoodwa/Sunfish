using Microsoft.Extensions.Configuration;

using Sunfish.Bridge;

using Xunit;

namespace Sunfish.Bridge.Tests.Unit;

/// <summary>
/// Validates that <see cref="BridgeOptions"/> binds correctly from
/// configuration — the linchpin of the ADR 0026 posture switch.
/// </summary>
public class BridgeModeTests
{
    private static BridgeOptions BindFrom(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new BridgeOptions();
        config.GetSection(BridgeOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Default_Mode_Is_SaaS_When_Config_Is_Empty()
    {
        var options = BindFrom(new Dictionary<string, string?>());
        Assert.Equal(BridgeMode.SaaS, options.Mode);
        Assert.Equal(500, options.Relay.MaxConnectedNodes);
        Assert.Empty(options.Relay.AllowedTeamIds);
    }

    [Fact]
    public void Mode_Binds_To_Relay_From_Config_String()
    {
        var options = BindFrom(new Dictionary<string, string?>
        {
            ["Bridge:Mode"] = "Relay",
        });
        Assert.Equal(BridgeMode.Relay, options.Mode);
    }

    [Fact]
    public void Relay_Subsection_Binds_Scalar_Knobs()
    {
        var options = BindFrom(new Dictionary<string, string?>
        {
            ["Bridge:Mode"] = "Relay",
            ["Bridge:Relay:ListenEndpoint"] = "tcp://0.0.0.0:8765",
            ["Bridge:Relay:MaxConnectedNodes"] = "42",
            ["Bridge:Relay:AdvertiseHostname"] = "relay.example.com",
        });
        Assert.Equal(BridgeMode.Relay, options.Mode);
        Assert.Equal("tcp://0.0.0.0:8765", options.Relay.ListenEndpoint);
        Assert.Equal(42, options.Relay.MaxConnectedNodes);
        Assert.Equal("relay.example.com", options.Relay.AdvertiseHostname);
    }

    [Fact]
    public void Relay_AllowedTeamIds_Binds_As_Array()
    {
        var options = BindFrom(new Dictionary<string, string?>
        {
            ["Bridge:Mode"] = "Relay",
            ["Bridge:Relay:AllowedTeamIds:0"] = "team-alpha",
            ["Bridge:Relay:AllowedTeamIds:1"] = "team-beta",
        });
        Assert.Equal(2, options.Relay.AllowedTeamIds.Length);
        Assert.Contains("team-alpha", options.Relay.AllowedTeamIds);
        Assert.Contains("team-beta", options.Relay.AllowedTeamIds);
    }
}
