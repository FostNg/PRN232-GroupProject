namespace RoomRum.BLL.DTOs.Responses;

public class ConversationResponse
{
    public Guid ConversationId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public ChatLastMessageResponse? LastMessage { get; set; }
    public Guid? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
}
