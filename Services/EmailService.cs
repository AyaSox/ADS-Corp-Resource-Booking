using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ResourceBooking.Models;
using Microsoft.Extensions.Options;

namespace ResourceBooking.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly CompanySettings _companySettings;
        private readonly ILogger<EmailService> _logger;
        private readonly INotificationService _notificationService;

        public EmailService(
            IOptions<EmailSettings> emailSettings, 
            IOptions<CompanySettings> companySettings,
            ILogger<EmailService> logger, 
            INotificationService notificationService)
        {
            _emailSettings = emailSettings.Value;
            _companySettings = companySettings.Value;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task SendWelcomeEmailAsync(ApplicationUser user)
        {
            var subject = $"Welcome to {_companySettings.CompanyName} Resource Management System!";
            var body = GenerateWelcomeEmailBody(user);
            
            await SendEmailAsync(user.Email, subject, body);
            
            // Also create in-app notification
            await _notificationService.CreateWelcomeNotificationAsync(user);
            
            _logger.LogInformation("Welcome email sent to {Email}", user.Email);
        }

        public async Task SendBookingConfirmationAsync(Booking booking)
        {
            if (booking.User == null || booking.Resource == null) return;

            var subject = $"[{_companySettings.CompanyName}] Booking Confirmed: {booking.Resource.Name}";
            var body = GenerateBookingConfirmationBody(booking);
            
            await SendEmailAsync(booking.User.Email, subject, body);
            
            // Also create in-app notification
            await _notificationService.CreateBookingConfirmationNotificationAsync(booking);
            
            _logger.LogInformation("Booking confirmation email sent to {Email} for booking {BookingId}", 
                booking.User.Email, booking.Id);
        }

        public async Task SendBookingCancellationAsync(Booking booking)
        {
            if (booking.User == null || booking.Resource == null) return;

            var subject = $"[{_companySettings.CompanyName}] Booking Cancelled: {booking.Resource.Name}";
            var body = GenerateBookingCancellationBody(booking);
            
            await SendEmailAsync(booking.User.Email, subject, body);
            
            // Also create in-app notification
            await _notificationService.CreateBookingCancellationNotificationAsync(booking);
            
            _logger.LogInformation("Booking cancellation email sent to {Email} for booking {BookingId}", 
                booking.User.Email, booking.Id);
        }

        public async Task SendTestEmailAsync(string toEmail, string subject, string body)
        {
            await SendEmailAsync(toEmail, subject, body);
            _logger.LogInformation("Test email sent to {Email}", toEmail);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = StripHtml(htmlBody)
                };
                message.Body = bodyBuilder.ToMessageBody();

                // Dev-friendly: if SMTP not configured, just log preview
                var smtpMissing = string.IsNullOrWhiteSpace(_emailSettings.SmtpServer) || string.IsNullOrWhiteSpace(_emailSettings.Username);
                if (smtpMissing || _emailSettings.Username == "your-email@gmail.com")
                {
                    _logger.LogInformation("EMAIL PREVIEW - To: {To}, Subject: {Subject}", toEmail, subject);
                    _logger.LogInformation("EMAIL CONTENT (HTML):\n{Body}", htmlBody);
                    return;
                }

                using var client = new SmtpClient
                {
                    Timeout = 15000 // 15s
                };

                var secure = _emailSettings.EnableSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, secure);
                
                if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
                {
                    await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}: {Message}", toEmail, ex.Message);
                throw;
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            try
            {
                // a very simple fallback if no HTML support
                var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
                return System.Net.WebUtility.HtmlDecode(text);
            }
            catch
            {
                return html;
            }
        }

        private string GenerateWelcomeEmailBody(ApplicationUser user)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Welcome to {_companySettings.CompanyName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f5f5f5; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; box-shadow: 0 4px 12px rgba(0,0,0,0.08); }}
        .header {{ background: #2563eb; color: white; text-align: center; padding: 28px 20px; }}
        .content {{ padding: 24px 20px; }}
        .button {{ display:inline-block; background:#16a34a; color:#fff !important; padding:12px 24px; text-decoration:none; border-radius:6px; }}
        .footer {{ background-color:#f3f4f6; color:#6b7280; text-align:center; padding:16px; font-size:12px; }}
        .card {{ background:#fff; border:1px solid #e5e7eb; border-radius:8px; padding:16px; margin:16px 0; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div style='font-size:20px; font-weight:700;'>{_companySettings.CompanyName}</div>
            <div style='opacity:.9;'>Resource Management System</div>
        </div>
        <div class='content'>
            <div class='card'>
                <h2 style='margin:0 0 8px 0;'>Welcome!</h2>
                <p style='margin:0 0 12px 0;'>Hello <strong>{user.FullName}</strong>, your account is ready. You can now book rooms, vehicles, and equipment.</p>
                <p style='margin:0 0 16px 0;'>Use the dashboard to create and manage your bookings.</p>
                <a class='button' href='{_companySettings.WebsiteUrl}'>Open Dashboard</a>
            </div>
            <div class='card'>
                <strong>Support</strong>
                <p style='margin:8px 0 0 0;'>Email: {_companySettings.SupportEmail} • Phone: {_companySettings.Phone}</p>
            </div>
        </div>
        <div class='footer'>
            <div><strong>{_companySettings.CompanyFullName}</strong></div>
            <div>{_companySettings.Address}</div>
            <div>&copy; {DateTime.Now.Year} {_companySettings.CompanyFullName}</div>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateBookingConfirmationBody(Booking booking)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; text-align: center; padding: 16px; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 24px; border-radius: 0 0 5px 5px; }}
        .booking-details {{ background-color: white; padding: 16px; border-radius: 5px; margin: 16px 0; border-left: 4px solid #28a745; }}
        .detail-row {{ display: flex; justify-content: space-between; margin: 8px 0; padding: 4px 0; border-bottom: 1px solid #eee; }}
        .button {{ display: inline-block; background-color: #007bff; color: white; padding: 10px 18px; text-decoration: none; border-radius: 5px; margin: 12px 0; }}
        .footer {{ text-align: center; margin-top: 16px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin:0;'>Booking Confirmed</h2>
        </div>
        <div class='content'>
            <p>Hello {booking.User.FullName}, your booking is confirmed.</p>
            <div class='booking-details'>
                <div class='detail-row'><strong>Resource:</strong><span>{booking.Resource.Name}</span></div>
                <div class='detail-row'><strong>Location:</strong><span>{booking.Resource.Location}</span></div>
                <div class='detail-row'><strong>Start:</strong><span>{booking.LocalStartTime:MMM dd, yyyy HH:mm}</span></div>
                <div class='detail-row'><strong>End:</strong><span>{booking.LocalEndTime:MMM dd, yyyy HH:mm}</span></div>
                <div class='detail-row'><strong>Purpose:</strong><span>{booking.Purpose}</span></div>
                <div class='detail-row'><strong>Booking ID:</strong><span>#{booking.Id}</span></div>
            </div>
            <a href='{_companySettings.WebsiteUrl}' class='button'>View my bookings</a>
            <p class='footer'><strong>{_companySettings.CompanyFullName}</strong><br/>{_companySettings.Address}<br/>Phone: {_companySettings.Phone} • Email: {_companySettings.SupportEmail}</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateBookingCancellationBody(Booking booking)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; text-align: center; padding: 16px; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 24px; border-radius: 0 0 5px 5px; }}
        .booking-details {{ background-color: white; padding: 16px; border-radius: 5px; margin: 16px 0; border-left: 4px solid #dc3545; }}
        .detail-row {{ display: flex; justify-content: space-between; margin: 8px 0; padding: 4px 0; border-bottom: 1px solid #eee; }}
        .button {{ display: inline-block; background-color: #007bff; color: white; padding: 10px 18px; text-decoration: none; border-radius: 5px; margin: 12px 0; }}
        .footer {{ text-align: center; margin-top: 16px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin:0;'>Booking Cancelled</h2>
        </div>
        <div class='content'>
            <p>Hello {booking.User.FullName}, your booking has been cancelled.</p>
            <div class='booking-details'>
                <div class='detail-row'><strong>Resource:</strong><span>{booking.Resource.Name}</span></div>
                <div class='detail-row'><strong>Location:</strong><span>{booking.Resource.Location}</span></div>
                <div class='detail-row'><strong>Start:</strong><span>{booking.LocalStartTime:MMM dd, yyyy HH:mm}</span></div>
                <div class='detail-row'><strong>End:</strong><span>{booking.LocalEndTime:MMM dd, yyyy HH:mm}</span></div>
                <div class='detail-row'><strong>Purpose:</strong><span>{booking.Purpose}</span></div>
                <div class='detail-row'><strong>Booking ID:</strong><span>#{booking.Id}</span></div>
            </div>
            <a href='{_companySettings.WebsiteUrl}' class='button'>Make a new booking</a>
            <p class='footer'><strong>{_companySettings.CompanyFullName}</strong><br/>{_companySettings.Address}<br/>Phone: {_companySettings.Phone} • Email: {_companySettings.SupportEmail}</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}