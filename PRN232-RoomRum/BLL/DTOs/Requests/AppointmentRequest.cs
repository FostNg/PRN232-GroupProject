namespace RoomRum.BLL.DTOs.Requests;

public class AppointmentRequest
{
    public Guid RoomId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? Description { get; set; }
}
