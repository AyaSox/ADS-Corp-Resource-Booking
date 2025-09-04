using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public interface INotificationService
    {
        Task CreateWelcomeNotificationAsync(ApplicationUser user);
        Task CreateBookingConfirmationNotificationAsync(Booking booking);
        Task CreateBookingCancellationNotificationAsync(Booking booking);
        Task CreateSystemNotificationAsync(string userId, string title, string message, int? bookingId = null);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int take = 50);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteNotificationAsync(int notificationId, string userId);
    }
}