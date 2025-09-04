using System.ComponentModel.DataAnnotations;

namespace ResourceBooking.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(1000)]
        public string Message { get; set; }

        public NotificationType Type { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optional: Reference to related booking
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }

        // Navigation property
        public ApplicationUser User { get; set; }

        // Helper properties
        public string TypeIcon => Type switch
        {
            NotificationType.Welcome => "bi-person-plus",
            NotificationType.BookingConfirmation => "bi-check-circle",
            NotificationType.BookingCancellation => "bi-x-circle",
            NotificationType.BookingReminder => "bi-clock",
            NotificationType.System => "bi-info-circle",
            _ => "bi-bell"
        };

        public string TypeColor => Type switch
        {
            NotificationType.Welcome => "text-primary",
            NotificationType.BookingConfirmation => "text-success",
            NotificationType.BookingCancellation => "text-danger",
            NotificationType.BookingReminder => "text-warning",
            NotificationType.System => "text-info",
            _ => "text-secondary"
        };

        public string RelativeTime
        {
            get
            {
                var timeSpan = DateTime.UtcNow - CreatedAt;
                
                if (timeSpan.TotalMinutes < 1)
                    return "Just now";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays}d ago";
                
                return CreatedAt.ToLocalTime().ToString("MMM dd");
            }
        }
    }

    public enum NotificationType
    {
        Welcome,
        BookingConfirmation,
        BookingCancellation,
        BookingReminder,
        System
    }
}