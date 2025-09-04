using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ResourceBooking.Helpers;

namespace ResourceBooking.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Resource")]
        public int ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public Resource Resource { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        [Required]
        [Display(Name = "End Time")]
        public DateTime EndTime { get; set; }

        [Required]
        [StringLength(500)]
        public string Purpose { get; set; }

        public bool Cancelled { get; set; } = false;

        // Link to ApplicationUser
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; }

        // New recurring fields
        public bool IsRecurring { get; set; } = false;
        public RecurrenceType? RecurrenceType { get; set; }
        public int? RecurrenceInterval { get; set; } = 1; // Every X weeks/days
        public DateTime? RecurrenceEndDate { get; set; }
        public int? ParentBookingId { get; set; } // For linking recurring bookings
        public Booking? ParentBooking { get; set; }
        public ICollection<Booking> ChildBookings { get; set; } = new List<Booking>();

        // Helper properties
        [Display(Name = "Booked By")]
        public string BookedByName => User?.FullName ?? "Unknown User";

        [NotMapped]
        public DateTime LocalStartTime => TimeZoneHelper.ConvertToLocal(StartTime);

        [NotMapped]
        public DateTime LocalEndTime => TimeZoneHelper.ConvertToLocal(EndTime);

        [NotMapped]
        public string TimeRange => $"{LocalStartTime:HH:mm} - {LocalEndTime:HH:mm}";

        [NotMapped]
        public string FullTimeRange => $"{LocalStartTime:MMM dd, yyyy HH:mm} - {LocalEndTime:MMM dd, yyyy HH:mm}";

        public string Duration
        {
            get
            {
                var duration = EndTime - StartTime;
                if (duration.TotalDays >= 1)
                    return $"{duration.TotalDays:F1} days";
                else if (duration.TotalHours >= 1)
                    return $"{duration.TotalHours:F1} hours";
                else
                    return $"{duration.TotalMinutes:F0} minutes";
            }
        }

        public string RecurrenceDisplay
        {
            get
            {
                if (!IsRecurring || !RecurrenceType.HasValue)
                    return "One-time";

                var interval = RecurrenceInterval ?? 1;
                var type = RecurrenceType.Value.ToString().ToLower();
                
                if (interval == 1)
                    return $"Every {type.TrimEnd('s')}";
                else
                    return $"Every {interval} {type}";
            }
        }

        public bool IsInPast => LocalEndTime < DateTime.Now;
        public bool IsToday => LocalStartTime.Date == DateTime.Today;
        public bool IsUpcoming => LocalStartTime > DateTime.Now;
    }

    public enum RecurrenceType
    {
        Daily,
        Weekly,
        Monthly
    }
}