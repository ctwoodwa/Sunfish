using Microsoft.AspNetCore.SignalR;

namespace Marilo.PmDemo.Hubs;

public sealed class PmDemoHub : Hub<IPmDemoHubClient>
{
    public Task JoinProject(string projectId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));

    public Task LeaveProject(string projectId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));

    public Task BroadcastTaskUpdate(string projectId, object payload)
        => Clients.OthersInGroup(GroupName(projectId)).TaskUpdated(payload);

    private static string GroupName(string projectId) => $"project:{projectId}";
}
