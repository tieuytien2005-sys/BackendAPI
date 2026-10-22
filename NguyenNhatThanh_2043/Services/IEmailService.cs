namespace NguyenNhatThanh_2043.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName);
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
