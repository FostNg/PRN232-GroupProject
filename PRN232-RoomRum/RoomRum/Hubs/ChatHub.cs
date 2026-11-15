using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RoomRum.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub;
