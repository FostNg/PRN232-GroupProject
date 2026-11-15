using Microsoft.AspNetCore.SignalR;

namespace RoomRum.API.Hubs;

// Only broadcasts that room data changed; clients fetch data through existing authorized APIs.
public sealed class RoomHub : Hub;
