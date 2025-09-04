using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Models;

namespace ResourceBooking.Data
{
    public class IdentityDataSeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<IdentityDataSeeder> _logger;

        public IdentityDataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<IdentityDataSeeder> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                // Create demo users if they don't exist
                await CreateDemoUsersAsync();
                
                // Clean up old bookings that don't have valid users
                await CleanupOldBookingsAsync();
                
                // Create sample bookings for demo users
                await CreateSampleBookingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding identity data: {Message}", ex.Message);
            }
        }

        private async Task CreateDemoUsersAsync()
        {
            var demoUsers = new[]
            {
                new { Email = "sipho@company.com", FirstName = "Sipho", LastName = "Mthembu", Password = "Demo123!" },
                new { Email = "thabo@company.com", FirstName = "Thabo", LastName = "Mokoena", Password = "Demo123!" },
                new { Email = "amanda@company.com", FirstName = "Amanda", LastName = "van der Merwe", Password = "Demo123!" }
            };

            foreach (var userInfo in demoUsers)
            {
                var existingUser = await _userManager.FindByEmailAsync(userInfo.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = userInfo.Email,
                        Email = userInfo.Email,
                        FirstName = userInfo.FirstName,
                        LastName = userInfo.LastName,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, userInfo.Password);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Created demo user: {Email}", userInfo.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create demo user {Email}: {Errors}", 
                            userInfo.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private async Task CleanupOldBookingsAsync()
        {
            try
            {
                // Remove any bookings that don't have a valid UserId or have old string-based BookedBy
                var invalidBookings = await _db.Bookings
                    .Where(b => string.IsNullOrEmpty(b.UserId))
                    .ToListAsync();

                if (invalidBookings.Any())
                {
                    _db.Bookings.RemoveRange(invalidBookings);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} invalid bookings", invalidBookings.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during booking cleanup: {Message}", ex.Message);
            }
        }

        private async Task CreateSampleBookingsAsync()
        {
            // Only create sample bookings if none exist
            if (await _db.Bookings.AnyAsync())
                return;

            var sipho = await _userManager.FindByEmailAsync("sipho@company.com");
            var thabo = await _userManager.FindByEmailAsync("thabo@company.com");
            
            if (sipho == null || thabo == null)
                return;

            var resources = await _db.Resources.ToListAsync();
            if (!resources.Any())
                return;

            var sampleBookings = new[]
            {
                new Booking
                {
                    ResourceId = resources.First().Id,
                    UserId = sipho.Id,
                    StartTime = DateTime.UtcNow.Date.AddHours(9),
                    EndTime = DateTime.UtcNow.Date.AddHours(10),
                    Purpose = "Team Stand-up Meeting",
                    Cancelled = false
                },
                new Booking
                {
                    ResourceId = resources.Skip(1).FirstOrDefault()?.Id ?? resources.First().Id,
                    UserId = thabo.Id,
                    StartTime = DateTime.UtcNow.Date.AddDays(1).AddHours(14),
                    EndTime = DateTime.UtcNow.Date.AddDays(1).AddHours(16),
                    Purpose = "Client Visit",
                    Cancelled = false
                }
            };

            _db.Bookings.AddRange(sampleBookings);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Created {Count} sample bookings", sampleBookings.Length);
        }
    }
}