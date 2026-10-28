namespace RoomRum.BLL.DTOs.Responses;

public class AppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? RoomTitle { get; set; }
    public string GuestName { get; set; } = string.Empty;
}
