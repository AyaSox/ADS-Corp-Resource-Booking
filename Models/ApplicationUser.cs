using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ResourceBooking.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}".Trim();

        // Navigation property for user's bookings
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}