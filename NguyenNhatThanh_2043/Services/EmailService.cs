using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;

namespace NguyenNhatThanh_2043.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName)
    {
        var subject = $"[{otpCode}] Mã OTP xác thực đăng nhập tài khoản";
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "Quý khách" : recipientName);

        var htmlBody = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Mã OTP Xác Thực</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">
    <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f1f5f9; padding: 30px 15px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 560px; background-color: #ffffff; border-radius: 16px; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.05); overflow: hidden; border: 1px solid #e2e8f0;"">
                    
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); padding: 32px 30px; text-align: center;"">
                            <h1 style=""color: #ffffff; font-size: 22px; margin: 0; font-weight: 700; letter-spacing: 0.5px;"">
                                XÁC THỰC BẢO MẬT 2 BƯỚC (OTP)
                            </h1>
                            <p style=""color: #bfdbfe; font-size: 13px; margin: 8px 0 0; text-transform: uppercase; letter-spacing: 1px;"">
                                NguyenNhatThanh_2043 Security System
                            </p>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 32px 30px;"">
                            <p style=""font-size: 16px; color: #1e293b; margin: 0 0 16px; line-height: 1.5;"">
                                Xin chào <strong>{safeName}</strong>,
                            </p>
                            <p style=""font-size: 15px; color: #475569; margin: 0 0 24px; line-height: 1.6;"">
                                Hệ thống vừa nhận được yêu cầu đăng nhập vào tài khoản của bạn. Vui lòng sử dụng mã xác thực bảo mật (OTP) dưới đây để tiếp tục:
                            </p>

                            <!-- OTP Box -->
                            <div style=""background-color: #f8fafc; border: 2px dashed #93c5fd; border-radius: 12px; padding: 22px 15px; text-align: center; margin: 25px 0;"">
                                <span style=""font-size: 38px; font-weight: 800; letter-spacing: 10px; color: #1d4ed8; font-family: 'Courier New', Courier, monospace;"">
                                    {otpCode}
                                </span>
                                <div style=""margin-top: 8px; font-size: 13px; color: #64748b;"">
                                    Mã này có hiệu lực trong vòng <strong style=""color: #dc2626;"">5 phút</strong>
                                </div>
                            </div>

                            <!-- Security Warnings -->
                            <div style=""background-color: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 6px; margin: 20px 0;"">
                                <p style=""font-size: 13px; color: #92400e; margin: 0; line-height: 1.5;"">
                                    🔒 <strong>Cảnh báo an toàn:</strong> Không chia sẻ mã OTP này cho bất kỳ ai, kể cả nhân viên quản trị. Nếu bạn không yêu cầu đăng nhập, vui lòng đổi mật khẩu ngay lập tức.
                                </p>
                            </div>

                            <p style=""font-size: 14px; color: #64748b; margin: 24px 0 0; line-height: 1.5;"">
                                Trân trọng,<br>
                                <strong>Đội ngũ Quản trị Hệ thống</strong>
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8fafc; padding: 18px 30px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;"">
                            Đây là email tự động từ hệ thống. Vui lòng không trả lời thư này.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        var port = int.TryParse(_configuration["EmailSettings:Port"], out var p) ? p : 587;
        var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "huamoc2005@gmail.com";
        var senderName = _configuration["EmailSettings:SenderName"] ?? "NguyenNhatThanh_2043 Security";
        var appPassword = (_configuration["EmailSettings:AppPassword"] ?? "").Trim().Replace(" ", "");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = $"Mã OTP của bạn là: {subject}. Mã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai."
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation("Đang kết nối SMTP {Host}:{Port} để gửi email tới: {Recipient}...", smtpServer, port, toEmail);

            var socketOptions = port switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.Auto
            };

            await client.ConnectAsync(smtpServer, port, socketOptions);
            await client.AuthenticateAsync(senderEmail, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Gửi email thành công tới {Recipient}!", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email qua SMTP ({Host}:{Port}) tới {Recipient}: {Message}", smtpServer, port, toEmail, ex.Message);
            throw;
        }
    }
}
