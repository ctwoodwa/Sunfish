namespace Sunfish.Bridge.Hubs;

public interface IBridgeHubClient
{
    Task TaskUpdated(object payload);

    // Phase 4 — Crew Comms thread messaging.
    Task ReceiveMessage(string threadId, string sender, string text, string timestamp);
}
