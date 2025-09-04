using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ResourceBooking.Models
{
    public class Resource
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(200)]
        public string Location { get; set; }

        [Range(1, 1000, ErrorMessage = "Capacity must be at least 1")]
        public int Capacity { get; set; }

        public bool IsAvailable { get; set; } = true;

        // New fields for unavailability
        [StringLength(500)]
        public string? UnavailabilityReason { get; set; }

        public DateTime? UnavailableUntil { get; set; }

        public UnavailabilityType? UnavailabilityType { get; set; }

        // Navigation property
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        // Helper properties
        public string StatusDisplay => IsAvailable ? "Available" : GetUnavailabilityDisplay();

        public string StatusBadgeClass => IsAvailable ? "bg-success" : GetUnavailabilityBadgeClass();

        public bool IsTemporarilyUnavailable => !IsAvailable && UnavailableUntil.HasValue;
        public bool IsPermanentlyDeactivated => !IsAvailable && !UnavailableUntil.HasValue && 
            (UnavailabilityType == Models.UnavailabilityType.DoesNotExist || 
             UnavailabilityType == Models.UnavailabilityType.AddedMistakenly || 
             UnavailabilityType == Models.UnavailabilityType.Retired);

        private string GetUnavailabilityDisplay()
        {
            if (!IsAvailable)
            {
                var reason = UnavailabilityType?.ToString() ?? "Unavailable";
                if (UnavailableUntil.HasValue)
                {
                    return $"{reason} until {UnavailableUntil.Value.ToLocalTime():MMM dd, yyyy}";
                }
                return reason;
            }
            return "Available";
        }

        private string GetUnavailabilityBadgeClass()
        {
            if (!IsAvailable && UnavailabilityType.HasValue)
            {
                return UnavailabilityType switch
                {
                    Models.UnavailabilityType.Maintenance => "bg-warning text-dark",
                    Models.UnavailabilityType.Repairs => "bg-danger",
                    Models.UnavailabilityType.Damaged => "bg-danger",
                    Models.UnavailabilityType.Cleaning => "bg-info",
                    Models.UnavailabilityType.Renovation => "bg-secondary",
                    Models.UnavailabilityType.DoesNotExist => "bg-dark text-white",
                    Models.UnavailabilityType.AddedMistakenly => "bg-dark text-white",
                    Models.UnavailabilityType.Retired => "bg-secondary",
                    Models.UnavailabilityType.Other => "bg-warning text-dark",
                    _ => "bg-warning text-dark"
                };
            }
            return "bg-success";
        }
    }

    public enum UnavailabilityType
    {
        // Temporary unavailability
        Maintenance,
        Repairs,
        Damaged,
        Cleaning,
        Renovation,
        Other,
        
        // Permanent deactivation reasons
        DoesNotExist,
        AddedMistakenly,
        Retired
    }
}