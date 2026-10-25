namespace RoomRum.BLL.Contracts;

public sealed record ChatLastMessageResponse(string? Content, DateTime? ChatAt);
public sealed record ConversationSummaryResponse(
    Guid ConversationId, DateTime? CreatedAt, ChatLastMessageResponse? LastMessage,
    Guid? OtherUserId, string? OtherUserName);

public sealed record ChatMessageResponse(
    Guid MessageId, Guid ConversationId, Guid Sender, Guid Receiver,
    string? Content, DateTime? ChatAt);

public sealed record ChatMessageCreatedResponse(Guid MessageId, Guid ConversationId);
