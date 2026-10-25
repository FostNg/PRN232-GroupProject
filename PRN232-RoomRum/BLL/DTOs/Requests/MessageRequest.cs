namespace RoomRum.BLL.DTOs.Requests;

public sealed record MessageRequest(Guid? ConversationId, Guid Receiver, string Content);
