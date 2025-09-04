using System;
using System.ComponentModel.DataAnnotations;
using ResourceBooking.Helpers;

namespace ResourceBooking.Models
{
    public class CreateBookingViewModel
    {
        [Required(ErrorMessage = "Please select a resource")]
        public int ResourceId { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public DateTime StartTime { get; set; } = DateTime.Now.RoundToNearestQuarterHour().AddHours(1);

        [Required(ErrorMessage = "End time is required")]
        public DateTime EndTime { get; set; } = DateTime.Now.RoundToNearestQuarterHour().AddHours(2);

        [Required(ErrorMessage = "Purpose is required")]
        [StringLength(500, ErrorMessage = "Purpose cannot exceed 500 characters")]
        public string Purpose { get; set; } = string.Empty;

        // Recurring booking fields
        public bool IsRecurring { get; set; } = false;

        public RecurrenceType? RecurrenceType { get; set; }

        [Range(1, 30, ErrorMessage = "Interval must be between 1 and 30")]
        public int RecurrenceInterval { get; set; } = 1;

        public DateTime? RecurrenceEndDate { get; set; }

        public Booking ToBooking(string userId)
        {
            var booking = new Booking
            {
                UserId = userId,
                ResourceId = ResourceId,
                StartTime = TimeZoneHelper.ConvertToUtc(StartTime),
                EndTime = TimeZoneHelper.ConvertToUtc(EndTime),
                Purpose = Purpose,
                IsRecurring = IsRecurring,
                RecurrenceType = IsRecurring ? RecurrenceType : null,
                RecurrenceInterval = IsRecurring ? RecurrenceInterval : null,
                RecurrenceEndDate = IsRecurring && RecurrenceEndDate.HasValue ? 
                    TimeZoneHelper.ConvertToUtc(RecurrenceEndDate.Value) : null
            };

            return booking;
        }

        // Validation method
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (EndTime <= StartTime)
                errors.Add("End time must be after start time");

            if (IsRecurring)
            {
                if (!RecurrenceType.HasValue)
                    errors.Add("Recurrence type is required for recurring bookings");

                if (!RecurrenceEndDate.HasValue)
                    errors.Add("End date is required for recurring bookings");
                else if (RecurrenceEndDate.Value <= StartTime)
                    errors.Add("Recurrence end date must be after the start time");

                if (RecurrenceInterval < 1 || RecurrenceInterval > 30)
                    errors.Add("Recurrence interval must be between 1 and 30");
            }

            return errors.Count == 0;
        }
    }
}

// Extension method to round DateTime to nearest quarter hour
public static class DateTimeExtensions
{
    public static DateTime RoundToNearestQuarterHour(this DateTime dateTime)
    {
        var minutes = dateTime.Minute;
        var roundedMinutes = (int)(Math.Round(minutes / 15.0) * 15);
        
        if (roundedMinutes == 60)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0).AddHours(1);
        }
        
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, roundedMinutes, 0);
    }
}