namespace RoomRum.BLL.DTOs.Responses;

public class ChatMessageResponse
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid Sender { get; set; }
    public Guid Receiver { get; set; }
    public string? Content { get; set; }
    public DateTime? ChatAt { get; set; }
}
