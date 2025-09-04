using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public class CalculationService : ICalculationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CalculationService> _logger;

        // Business hours configuration
        private const int BusinessStartHour = 8;  // 8 AM
        private const int BusinessEndHour = 18;   // 6 PM
        private const int BusinessHoursPerDay = BusinessEndHour - BusinessStartHour; // 10 hours
        private readonly int[] BusinessDays = { 1, 2, 3, 4, 5 }; // Monday to Friday

        public CalculationService(ApplicationDbContext db, ILogger<CalculationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ResourceUtilizationStats> GetResourceUtilizationAsync(int resourceId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var resource = await _db.Resources.FindAsync(resourceId);
                if (resource == null)
                    return new ResourceUtilizationStats { ResourceName = "Unknown" };

                var bookings = await _db.Bookings
                    .Where(b => b.ResourceId == resourceId && 
                               !b.Cancelled && 
                               b.StartTime >= startDate && 
                               b.EndTime <= endDate)
                    .ToListAsync();

                var totalHours = bookings.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                var totalPossibleHours = CalculateBusinessHours(startDate, endDate);
                var utilizationPercentage = totalPossibleHours > 0 ? (totalHours / totalPossibleHours) * 100 : 0;

                return new ResourceUtilizationStats
                {
                    ResourceName = resource.Name,
                    TotalBookings = bookings.Count,
                    TotalHoursBooked = Math.Round(totalHours, 2),
                    UtilizationPercentage = Math.Round(utilizationPercentage, 2),
                    AverageBookingDuration = bookings.Count > 0 ? 
                        Math.Round(bookings.Average(b => (b.EndTime - b.StartTime).TotalHours), 2) : 0,
                    MostPopularTimeSlot = bookings.Count > 0 ? 
                        bookings.GroupBy(b => b.StartTime.Hour)
                               .OrderByDescending(g => g.Count())
                               .First().First().StartTime : DateTime.MinValue
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating resource utilization for resource {ResourceId}", resourceId);
                return new ResourceUtilizationStats { ResourceName = "Error" };
            }
        }

        public async Task<UserBookingStats> GetUserBookingStatsAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return new UserBookingStats { UserName = "Unknown" };

                var bookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Where(b => b.UserId == userId && 
                               b.StartTime >= startDate && 
                               b.EndTime <= endDate)
                    .ToListAsync();

                var cancelledBookings = bookings.Where(b => b.Cancelled).ToList();
                var activeBookings = bookings.Where(b => !b.Cancelled).ToList();
                
                var totalHours = activeBookings.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                var mostUsedResource = activeBookings
                    .GroupBy(b => b.Resource.Name)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None";

                return new UserBookingStats
                {
                    UserName = user.FullName,
                    TotalBookings = bookings.Count, // Include all bookings (cancelled + active) for cancellation rate calculation
                    CancelledBookings = cancelledBookings.Count,
                    TotalHoursBooked = Math.Round(totalHours, 2),
                    AverageBookingDuration = activeBookings.Count > 0 ? 
                        Math.Round(activeBookings.Average(b => (b.EndTime - b.StartTime).TotalHours), 2) : 0,
                    MostUsedResource = mostUsedResource
                    // CancellationRate is calculated automatically in the property
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating user booking stats for user {UserId}", userId);
                return new UserBookingStats { UserName = "Error" };
            }
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfToday = today.AddDays(1);

                _logger.LogInformation("=== DASHBOARD STATS CALCULATION ===");

                // Get all resources
                var resources = await _db.Resources.ToListAsync();
                var unavailableResources = resources.Where(r => !r.IsAvailable).ToList();

                _logger.LogInformation("Found {TotalResources} total resources, {AvailableCount} available", 
                    resources.Count, resources.Count(r => r.IsAvailable));

                // Get today's bookings
                var todayBookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Include(b => b.User)
                    .Where(b => !b.Cancelled && 
                               b.StartTime >= today && 
                               b.StartTime < endOfToday)
                    .ToListAsync();

                // Get week bookings
                var weekBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && 
                               b.StartTime >= startOfWeek && 
                               b.StartTime < startOfWeek.AddDays(7))
                    .CountAsync();

                // Get month bookings
                var monthBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && 
                               b.StartTime >= startOfMonth && 
                               b.StartTime < startOfMonth.AddMonths(1))
                    .CountAsync();

                _logger.LogInformation("Bookings - Today: {Today}, Week: {Week}, Month: {Month}", 
                    todayBookings.Count, weekBookings, monthBookings);

                // Calculate average utilization for available resources
                var availableResources = resources.Where(r => r.IsAvailable).ToList();
                double avgUtilization = 0;
                
                if (availableResources.Any())
                {
                    // Get all bookings this month for utilization calculation
                    var monthBookingsForUtilization = await _db.Bookings
                        .Where(b => !b.Cancelled && 
                                   b.StartTime >= startOfMonth && 
                                   b.EndTime <= today.AddDays(1))
                        .ToListAsync();

                    if (monthBookingsForUtilization.Any())
                    {
                        var totalBookedHours = monthBookingsForUtilization.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                        var businessHoursThisMonth = CalculateBusinessHours(startOfMonth, today);
                        var totalPossibleHours = availableResources.Count * businessHoursThisMonth;
                        
                        avgUtilization = totalPossibleHours > 0 ? (totalBookedHours / totalPossibleHours) * 100 : 0;
                        
                        _logger.LogInformation("Utilization: {BookedHours}h booked / {PossibleHours}h possible = {Utilization}%", 
                            Math.Round(totalBookedHours, 1), Math.Round(totalPossibleHours, 1), Math.Round(avgUtilization, 1));
                    }
                    else
                    {
                        _logger.LogInformation("No bookings found for utilization calculation");
                    }
                }

                var dashboardStats = new DashboardStats
                {
                    TotalResources = resources.Count,
                    AvailableResources = resources.Count(r => r.IsAvailable),
                    UnavailableResources = unavailableResources.Count,
                    TotalBookingsToday = todayBookings.Count,
                    TotalBookingsThisWeek = weekBookings,
                    TotalBookingsThisMonth = monthBookings,
                    AverageUtilizationPercentage = Math.Round(avgUtilization, 0),
                    UnavailableResourcesInfo = unavailableResources.Select(r => new UnavailableResourceInfo
                    {
                        Name = r.Name,
                        Reason = r.UnavailabilityReason ?? "No reason specified",
                        Type = r.UnavailabilityType?.ToString() ?? "Unknown",
                        UnavailableUntil = r.UnavailableUntil,
                        BadgeClass = r.StatusBadgeClass
                    }).ToList(),
                    TodayBookings = todayBookings.Select(b => new TodayBookingInfo
                    {
                        ResourceName = b.Resource.Name,
                        UserName = b.User.FullName,
                        Purpose = b.Purpose,
                        StartTime = b.LocalStartTime,
                        EndTime = b.LocalEndTime,
                        IsRecurring = b.IsRecurring
                    }).ToList()
                };

                _logger.LogInformation("FINAL Dashboard Stats: Utilization = {Utilization}%", 
                    dashboardStats.AverageUtilizationPercentage);

                return dashboardStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating dashboard stats: {Message}", ex.Message);
                return new DashboardStats();
            }
        }

        public async Task<List<ResourcePopularityStats>> GetResourcePopularityStatsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var resources = await _db.Resources
                    .Include(r => r.Bookings.Where(b => !b.Cancelled && 
                                                       b.StartTime >= startDate && 
                                                       b.EndTime <= endDate))
                    .ToListAsync();

                var stats = resources
                    .Where(r => r.Bookings.Any())
                    .Select(r => new ResourcePopularityStats
                    {
                        ResourceName = r.Name,
                        BookingCount = r.Bookings.Count,
                        TotalHours = Math.Round(r.Bookings.Sum(b => (b.EndTime - b.StartTime).TotalHours), 2)
                    })
                    .OrderByDescending(s => s.BookingCount)
                    .ToList();

                // Calculate utilization percentages based on business hours
                var totalPossibleHours = CalculateBusinessHours(startDate, endDate);
                foreach (var stat in stats)
                {
                    stat.UtilizationPercentage = totalPossibleHours > 0 ? 
                        Math.Round((stat.TotalHours / totalPossibleHours) * 100, 2) : 0;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating resource popularity stats: {Message}", ex.Message);
                return new List<ResourcePopularityStats>();
            }
        }

        public async Task<double> CalculateResourceUtilizationPercentageAsync(int resourceId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var bookings = await _db.Bookings
                    .Where(b => b.ResourceId == resourceId && 
                               !b.Cancelled && 
                               b.StartTime >= startDate && 
                               b.EndTime <= endDate)
                    .ToListAsync();

                var totalBookedHours = bookings.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                var totalPossibleHours = CalculateBusinessHours(startDate, endDate);

                var utilization = totalPossibleHours > 0 ? (totalBookedHours / totalPossibleHours) * 100 : 0;
                
                _logger.LogInformation("Resource {ResourceId}: {BookedHours}h booked out of {PossibleHours}h available = {Utilization}%", 
                    resourceId, Math.Round(totalBookedHours, 2), Math.Round(totalPossibleHours, 2), Math.Round(utilization, 2));

                return utilization;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating utilization percentage for resource {ResourceId}: {Message}", 
                    resourceId, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Calculate total business hours between two dates (Monday-Friday, 8 AM - 6 PM)
        /// </summary>
        private double CalculateBusinessHours(DateTime startDate, DateTime endDate)
        {
            double totalHours = 0;
            var current = startDate.Date;

            while (current <= endDate.Date)
            {
                // Check if it's a weekday (Monday = 1, Friday = 5)
                if (BusinessDays.Contains((int)current.DayOfWeek))
                {
                    totalHours += BusinessHoursPerDay;
                }
                current = current.AddDays(1);
            }

            return totalHours;
        }
    }
}