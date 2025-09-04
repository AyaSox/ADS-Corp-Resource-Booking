using System;
using System.ComponentModel.DataAnnotations;
using ResourceBooking.Helpers;

namespace ResourceBooking.Models
{
    public class EditBookingViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a resource")]
        [Display(Name = "Resource")]
        public int ResourceId { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "End time is required")]
        [Display(Name = "End Time")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Purpose is required")]
        [StringLength(500, ErrorMessage = "Purpose cannot exceed 500 characters")]
        public string Purpose { get; set; }

        public bool Cancelled { get; set; }

        public string UserId { get; set; }

        // Create from existing Booking with proper timezone handling
        public static EditBookingViewModel FromBooking(Booking booking)
        {
            return new EditBookingViewModel
            {
                Id = booking.Id,
                ResourceId = booking.ResourceId,
                StartTime = TimeZoneHelper.ConvertToLocal(booking.StartTime), // Convert to local for editing
                EndTime = TimeZoneHelper.ConvertToLocal(booking.EndTime),     // Convert to local for editing
                Purpose = booking.Purpose,
                Cancelled = booking.Cancelled,
                UserId = booking.UserId
            };
        }

        // Update existing Booking entity with proper timezone handling
        public void UpdateBooking(Booking booking)
        {
            booking.ResourceId = ResourceId;
            booking.StartTime = TimeZoneHelper.ConvertToUtc(StartTime);  // Convert to UTC for storage
            booking.EndTime = TimeZoneHelper.ConvertToUtc(EndTime);      // Convert to UTC for storage
            booking.Purpose = Purpose;
            booking.Cancelled = Cancelled;
            // Don't update UserId - it should remain the same
        }
    }
}