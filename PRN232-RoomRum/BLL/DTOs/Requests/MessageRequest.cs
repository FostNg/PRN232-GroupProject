namespace RoomRum.BLL.DTOs.Requests;

public class MessageRequest
{
    public Guid? ConversationId { get; set; }
    public Guid Receiver { get; set; }
    public string Content { get; set; } = string.Empty;
}
