using Microsoft.AspNetCore.SignalR;

namespace Sunfish.Bridge.Hubs;

public sealed class BridgeHub : Hub<IBridgeHubClient>
{
    public Task JoinProject(string projectId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));

    public Task LeaveProject(string projectId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));

    public Task BroadcastTaskUpdate(string projectId, object payload)
        => Clients.OthersInGroup(GroupName(projectId)).TaskUpdated(payload);

    // Phase 4 — Crew Comms thread messaging (bridges blocks-crew-comms over SignalR).
    public Task JoinThread(string threadId)
        => Groups.AddToGroupAsync(Context.ConnectionId, ThreadGroupName(threadId));

    public Task LeaveThread(string threadId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, ThreadGroupName(threadId));

    public async Task SendMessage(string threadId, string text)
    {
        var sender = Context.User?.Identity?.Name ?? Context.ConnectionId;
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        await Clients.Group(ThreadGroupName(threadId))
            .ReceiveMessage(threadId, sender, text, timestamp)
            .ConfigureAwait(false);
    }

    private static string GroupName(string projectId) => $"project:{projectId}";
    private static string ThreadGroupName(string threadId) => $"thread:{threadId}";
}
