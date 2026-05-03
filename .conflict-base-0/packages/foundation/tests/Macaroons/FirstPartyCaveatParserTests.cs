using Sunfish.Foundation.Macaroons;

namespace Sunfish.Foundation.Tests.Macaroons;

public class FirstPartyCaveatParserTests
{
    private static MacaroonContext Ctx(
        DateTimeOffset? now = null,
        string? subject = null,
        string? schema = null,
        string? action = null,
        string? ip = null)
        => new(now ?? DateTimeOffset.UtcNow, subject, schema, action, ip);

    [Fact]
    public void TimeCaveat_AcceptsBeforeDeadline()
    {
        var caveat = new Caveat("time <= \"2099-01-01T00:00:00Z\"");
        var result = FirstPartyCaveatParser.Evaluate(
            caveat,
            Ctx(now: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.True(result);
    }

    [Fact]
    public void TimeCaveat_RejectsAfterDeadline()
    {
        var caveat = new Caveat("time <= \"2020-01-01T00:00:00Z\"");
        var result = FirstPartyCaveatParser.Evaluate(
            caveat,
            Ctx(now: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(result);
    }

    [Fact]
    public void SubjectCaveat_MatchesExactSubject()
    {
        var caveat = new Caveat("subject == \"urn:sunfish:alice\"");
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(subject: "urn:sunfish:alice")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(subject: "urn:sunfish:bob")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(subject: null)));
    }

    [Fact]
    public void SchemaGlob_MatchesWildcardPattern()
    {
        var caveat = new Caveat("resource.schema matches \"sunfish.pm.inspection/*\"");
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(schema: "sunfish.pm.inspection/apartment")));
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(schema: "sunfish.pm.inspection/house")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(schema: "sunfish.pm.workorder/task")));
    }

    [Fact]
    public void ActionInList_AcceptsListedAction()
    {
        var caveat = new Caveat("action in [\"read\", \"list\"]");
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(action: "read")));
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(action: "list")));
    }

    [Fact]
    public void ActionInList_RejectsUnlistedAction()
    {
        var caveat = new Caveat("action in [\"read\", \"list\"]");
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(action: "write")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(action: null)));
    }

    [Fact]
    public void DeviceIpInCidr_MatchesCorrectly()
    {
        var caveat = new Caveat("device_ip in \"10.42.0.0/16\"");
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(ip: "10.42.5.17")));
        Assert.True(FirstPartyCaveatParser.Evaluate(caveat, Ctx(ip: "10.42.0.1")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(ip: "10.43.0.1")));
        Assert.False(FirstPartyCaveatParser.Evaluate(caveat, Ctx(ip: null)));
    }

    [Fact]
    public void UnknownCaveat_FailsClosed()
    {
        Assert.False(FirstPartyCaveatParser.Evaluate(new Caveat("garbage predicate"), Ctx()));
        Assert.False(FirstPartyCaveatParser.Evaluate(new Caveat(""), Ctx()));
    }
}
