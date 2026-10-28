namespace RoomRum.BLL.DTOs.Responses;

public class VerificationEmailResponse
{
    public string Message { get; set; } = string.Empty;
    public string? EmailDelivery { get; set; }

    public VerificationEmailResponse(string message, string? emailDelivery)
    {
        Message = message;
        EmailDelivery = emailDelivery;
    }
}
