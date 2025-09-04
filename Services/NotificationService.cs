using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<NotificationService> _logger;
        private static readonly TimeSpan DefaultDedupWindow = TimeSpan.FromMinutes(2);

        public NotificationService(ApplicationDbContext db, ILogger<NotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        private async Task<bool> ExistsRecentAsync(string userId, string title, string message, TimeSpan? window = null, int? bookingId = null, NotificationType? type = null)
        {
            var since = DateTime.UtcNow - (window ?? DefaultDedupWindow);
            var q = _db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId && n.Title == title && n.Message == message && n.CreatedAt >= since);
            if (bookingId.HasValue) q = q.Where(n => n.BookingId == bookingId);
            if (type.HasValue) q = q.Where(n => n.Type == type);
            return await q.AnyAsync();
        }

        public async Task CreateWelcomeNotificationAsync(ApplicationUser user)
        {
            var title = "Welcome to Resource Booking!";
            var message = $"Hello {user.FullName}! Welcome to our Resource Booking System. You can now book conference rooms, vehicles, and equipment. Start by exploring available resources or making your first booking.";
            if (await ExistsRecentAsync(user.Id, title, message)) return;

            var notification = new Notification
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = NotificationType.Welcome
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Welcome notification created for user {UserId}", user.Id);
        }

        public async Task CreateBookingConfirmationNotificationAsync(Booking booking)
        {
            if (booking.User == null || booking.Resource == null) return;

            var title = "Booking Confirmed";
            var message = $"Your booking for '{booking.Resource.Name}' has been confirmed for {booking.LocalStartTime:MMM dd, yyyy} from {booking.LocalStartTime:HH:mm} to {booking.LocalEndTime:HH:mm}. Location: {booking.Resource.Location}";
            if (await ExistsRecentAsync(booking.UserId, title, message, DefaultDedupWindow, booking.Id, NotificationType.BookingConfirmation)) return;

            var notification = new Notification
            {
                UserId = booking.UserId,
                BookingId = booking.Id,
                Title = title,
                Message = message,
                Type = NotificationType.BookingConfirmation
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Booking confirmation notification created for user {UserId}, booking {BookingId}", booking.UserId, booking.Id);
        }

        public async Task CreateBookingCancellationNotificationAsync(Booking booking)
        {
            if (booking.User == null || booking.Resource == null) return;

            var title = "Booking Cancelled";
            var message = $"Your booking for '{booking.Resource.Name}' scheduled for {booking.LocalStartTime:MMM dd, yyyy} from {booking.LocalStartTime:HH:mm} to {booking.LocalEndTime:HH:mm} has been cancelled. The time slot is now available for others to book.";
            if (await ExistsRecentAsync(booking.UserId, title, message, DefaultDedupWindow, booking.Id, NotificationType.BookingCancellation)) return;

            var notification = new Notification
            {
                UserId = booking.UserId,
                BookingId = booking.Id,
                Title = title,
                Message = message,
                Type = NotificationType.BookingCancellation
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Booking cancellation notification created for user {UserId}, booking {BookingId}", booking.UserId, booking.Id);
        }

        public async Task CreateSystemNotificationAsync(string userId, string title, string message, int? bookingId = null)
        {
            if (await ExistsRecentAsync(userId, title, message, DefaultDedupWindow, bookingId, NotificationType.System)) return;

            var notification = new Notification
            {
                UserId = userId,
                BookingId = bookingId,
                Title = title,
                Message = message,
                Type = NotificationType.System
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            _logger.LogInformation("System notification '{Title}' created for user {UserId}", title, userId);
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int take = 50)
        {
            return await _db.Notifications
                .Include(n => n.Booking)
                    .ThenInclude(b => b.Resource)
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Notification {NotificationId} marked as read for user {UserId}", notificationId, userId);
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("All notifications marked as read for user {UserId}", userId);
        }

        public async Task DeleteNotificationAsync(int notificationId, string userId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null)
            {
                _db.Notifications.Remove(notification);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Notification {NotificationId} deleted for user {UserId}", notificationId, userId);
            }
        }
    }
}