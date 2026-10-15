using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index("ConversationId", "SentAt", "MessageId", Name = "IX_Messages_Conversation_SentAt")]
public partial class Message
{
    [Key]
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    [StringLength(2000)]
    public string Content { get; set; } = null!;

    [Precision(0)]
    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    [ForeignKey("ConversationId")]
    [InverseProperty("Messages")]
    public virtual Conversation Conversation { get; set; } = null!;

    [ForeignKey("SenderId")]
    [InverseProperty("Messages")]
    public virtual User Sender { get; set; } = null!;
}
