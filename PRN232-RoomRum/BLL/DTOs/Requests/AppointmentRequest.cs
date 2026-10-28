namespace RoomRum.BLL.DTOs.Requests;

public sealed record AppointmentRequest(Guid RoomId, DateOnly AppointmentDate, TimeOnly AppointmentTime, string? Description);
