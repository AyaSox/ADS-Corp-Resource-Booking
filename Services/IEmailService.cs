using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(ApplicationUser user);
        Task SendBookingConfirmationAsync(Booking booking);
        Task SendBookingCancellationAsync(Booking booking);
        Task SendTestEmailAsync(string toEmail, string subject, string body);
    }
}