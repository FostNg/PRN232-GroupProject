using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using RoomRum.BLL.Services.Interfaces;

namespace RoomRum.API.Registration;

public sealed class RegistrationEmailSender(IConfiguration config, IWebHostEnvironment environment,
    ILogger<RegistrationEmailSender> logger) : IRegistrationEmailSender
{
    public string DeliveryMethod => config["Email:DeliveryMethod"] ?? "Smtp";
    private string PickupPath => Path.GetFullPath(Path.Combine(environment.ContentRootPath,
        config["Email:PickupDirectory"] ?? "App_Data/maildrop"));

    public bool IsConfigured
    {
        get
        {
            if (DeliveryMethod == "PickupDirectory")
            {
                var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
                return environment.IsDevelopment() && !PickupPath.Equals(webRoot, StringComparison.OrdinalIgnoreCase) &&
                    !PickupPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            var gmail = string.Equals(config["Smtp:Host"], "smtp.gmail.com", StringComparison.OrdinalIgnoreCase);
            if (gmail && (config.GetValue("Smtp:Port", 587) != 587 || !config.GetValue("Smtp:EnableSsl", true) ||
                string.IsNullOrWhiteSpace(config["Smtp:Username"]) || string.IsNullOrWhiteSpace(config["Smtp:Password"])))
                return false;
            return DeliveryMethod == "Smtp" && !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
                MailAddress.TryCreate(config["Smtp:FromEmail"], out _) &&
                config.GetValue("Smtp:Port", 587) is > 0 and <= 65535 &&
                (environment.IsDevelopment() || config.GetValue("Smtp:EnableSsl", true)) &&
                (string.IsNullOrWhiteSpace(config["Smtp:Username"]) || !string.IsNullOrWhiteSpace(config["Smtp:Password"]));
        }
    }

    public async Task<bool> SendAsync(string email, string name, string verificationLink, int tokenMinutes)
    {
        if (!IsConfigured) return false;
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(config["Smtp:FromEmail"])
                    ? "noreply@roomrum.invalid" : config["Smtp:FromEmail"]!, "RoomRum"),
                Subject = "Verify your RoomRum email",
                Body = $"Verify your RoomRum account using this link (valid for {tokenMinutes} minutes):\r\n\r\n{verificationLink}\r\n\r\nIf you did not register, ignore this email.",
                BodyEncoding = Encoding.ASCII,
                BodyTransferEncoding = TransferEncoding.SevenBit
            };
            message.To.Add(new MailAddress(email));
            using var client = new SmtpClient();
            if (DeliveryMethod == "PickupDirectory")
            {
                Directory.CreateDirectory(PickupPath);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = PickupPath;
            }
            else
            {
                client.Host = config["Smtp:Host"]!;
                client.Port = config.GetValue("Smtp:Port", 587);
                // SmtpClient.EnableSsl uses STARTTLS (RFC 3207), including Gmail on port 587.
                client.EnableSsl = config.GetValue("Smtp:EnableSsl", true);
                client.UseDefaultCredentials = false;
                if (!string.IsNullOrWhiteSpace(config["Smtp:Username"]))
                    client.Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]);
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
            return true;
        }
        catch (Exception error) when (error is SmtpException or IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            // Never log verification links, tokens, SMTP credentials or email bodies.
            logger.LogWarning("Registration email delivery failed ({ErrorType}).", error.GetType().Name);
            return false;
        }
    }
}


