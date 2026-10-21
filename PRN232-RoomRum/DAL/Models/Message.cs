using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class Message
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid Sender { get; set; }

    public Guid Receiver { get; set; }

    public string? Content { get; set; }

    public DateTime? ChatAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User ReceiverNavigation { get; set; } = null!;

    public virtual User SenderNavigation { get; set; } = null!;
}
