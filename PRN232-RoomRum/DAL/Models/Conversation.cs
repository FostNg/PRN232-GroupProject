using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class Conversation
{
    public Guid ConversationId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
