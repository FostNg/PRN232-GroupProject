using Microsoft.AspNetCore.SignalR;

namespace RoomRum.API.Hubs;

public sealed class RoomUpdateNotifier(IHubContext<RoomHub> hub, ILogger<RoomUpdateNotifier> logger)
{
    public async Task NotifyAsync()
    {
        try
        {
            await hub.Clients.All.SendAsync("RoomsChanged");
        }
        catch (Exception error)
        {
            logger.LogError(error, "Room update notification failed after saving changes.");
        }
    }
}
