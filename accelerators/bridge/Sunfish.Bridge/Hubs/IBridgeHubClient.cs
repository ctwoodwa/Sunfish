namespace Sunfish.Bridge.Hubs;

public interface IBridgeHubClient
{
    Task TaskUpdated(object payload);
}
