using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public class CalculationService : ICalculationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CalculationService> _logger;

        // Business hours configuration (UTC-based)
        private const int BusinessStartHour = 8;  // 08:00
        private const int BusinessEndHour = 18;   // 18:00
        private const int BusinessHoursPerDay = BusinessEndHour - BusinessStartHour; // 10 hours
        private readonly int[] BusinessDays = { 1, 2, 3, 4, 5 }; // Monday=1..Friday=5

        public CalculationService(ApplicationDbContext db, ILogger<CalculationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ResourceUtilizationStats> GetResourceUtilizationAsync(int resourceId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var resource = await _db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resourceId);
                if (resource == null)
                    return new ResourceUtilizationStats { ResourceName = "Unknown" };

                // include overlapping bookings: b.Start < end AND b.End > start
                var bookings = await _db.Bookings
                    .AsNoTracking()
                    .Where(b => b.ResourceId == resourceId && !b.Cancelled && b.StartTime < endDate && b.EndTime > startDate)
                    .ToListAsync();

                // capacity within business hours for window
                var totalPossibleHours = CalculateBusinessHoursClipped(startDate, endDate);

                // sum booked minutes clipped to business hours and range
                double bookedMinutes = 0;
                foreach (var b in bookings)
                {
                    var s = b.StartTime < startDate ? startDate : b.StartTime;
                    var e = b.EndTime > endDate ? endDate : b.EndTime;
                    if (e <= s) continue;
                    bookedMinutes += CalculateBookedMinutesWithinBusinessHours(s, e);
                }

                var totalHoursBooked = Math.Round(bookedMinutes / 60.0, 2);
                var utilizationPercentage = totalPossibleHours > 0 ? Math.Round((totalHoursBooked / totalPossibleHours) * 100.0, 2) : 0;

                var avgDuration = bookings.Count > 0 ? Math.Round(bookings.Average(b => (b.EndTime - b.StartTime).TotalHours), 2) : 0.0;

                DateTime mostPopularSlot = DateTime.MinValue;
                if (bookings.Count > 0)
                {
                    var hour = bookings.GroupBy(b => b.StartTime.Hour)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key)
                        .Select(g => g.Key)
                        .First();
                    mostPopularSlot = new DateTime(startDate.Year, startDate.Month, startDate.Day, hour, 0, 0, DateTimeKind.Utc);
                }

                return new ResourceUtilizationStats
                {
                    ResourceName = resource.Name,
                    TotalBookings = bookings.Count,
                    TotalHoursBooked = totalHoursBooked,
                    UtilizationPercentage = utilizationPercentage,
                    AverageBookingDuration = avgDuration,
                    MostPopularTimeSlot = mostPopularSlot
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
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return new UserBookingStats { UserName = "Unknown" };

                var bookings = await _db.Bookings
                    .AsNoTracking()
                    .Include(b => b.Resource)
                    .Where(b => b.UserId == userId && b.StartTime >= startDate && b.StartTime < endDate)
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
                    TotalBookings = bookings.Count,
                    CancelledBookings = cancelledBookings.Count,
                    TotalHoursBooked = Math.Round(totalHours, 2),
                    AverageBookingDuration = activeBookings.Count > 0 ? Math.Round(activeBookings.Average(b => (b.EndTime - b.StartTime).TotalHours), 2) : 0,
                    MostUsedResource = mostUsedResource
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
                var utcToday = DateTime.UtcNow.Date;
                var startOfWeek = utcToday.AddDays(-(int)utcToday.DayOfWeek);
                var startOfMonth = new DateTime(utcToday.Year, utcToday.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfToday = utcToday.AddDays(1);

                _logger.LogInformation("=== DASHBOARD STATS CALCULATION (UTC) ===");

                var resources = await _db.Resources.AsNoTracking().ToListAsync();
                var unavailableResources = resources.Where(r => !r.IsAvailable).ToList();

                var todayBookings = await _db.Bookings
                    .AsNoTracking()
                    .Include(b => b.Resource)
                    .Include(b => b.User)
                    .Where(b => !b.Cancelled && b.StartTime >= utcToday && b.StartTime < endOfToday)
                    .ToListAsync();

                var weekBookings = await _db.Bookings.AsNoTracking()
                    .Where(b => !b.Cancelled && b.StartTime >= startOfWeek && b.StartTime < startOfWeek.AddDays(7))
                    .CountAsync();

                var monthBookings = await _db.Bookings.AsNoTracking()
                    .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.StartTime < startOfMonth.AddMonths(1))
                    .CountAsync();

                var availableResources = resources.Where(r => r.IsAvailable).ToList();
                double avgUtilization = 0;

                if (availableResources.Any())
                {
                    var monthBookingsForUtilization = await _db.Bookings.AsNoTracking()
                        .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.EndTime <= endOfToday)
                        .ToListAsync();

                    if (monthBookingsForUtilization.Any())
                    {
                        var totalBookedHours = monthBookingsForUtilization.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                        var daysInMonthSoFar = (utcToday - startOfMonth).Days + 1;
                        var totalPossibleHours = availableResources.Count * daysInMonthSoFar * BusinessHoursPerDay;
                        avgUtilization = totalPossibleHours > 0 ? (totalBookedHours / totalPossibleHours) * 100 : 0;
                    }
                }

                var activeUserCount = await _db.Bookings.AsNoTracking()
                    .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.StartTime < endOfToday)
                    .Select(b => b.UserId)
                    .Distinct()
                    .CountAsync();

                var dashboardStats = new DashboardStats
                {
                    TotalResources = resources.Count,
                    AvailableResources = resources.Count(r => r.IsAvailable),
                    UnavailableResources = unavailableResources.Count,
                    TotalBookingsToday = todayBookings.Count,
                    TotalBookingsThisWeek = weekBookings,
                    TotalBookingsThisMonth = monthBookings,
                    AverageUtilizationPercentage = Math.Round((double)avgUtilization, 0),
                    ActiveUsers = activeUserCount,
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
                        ResourceName = b.Resource?.Name ?? "Unknown Resource",
                        UserName = b.User?.FullName ?? "Unknown User",
                        Purpose = b.Purpose,
                        StartTime = b.LocalStartTime,
                        EndTime = b.LocalEndTime,
                        IsRecurring = b.IsRecurring
                    }).ToList()
                };

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
                    .AsNoTracking()
                    .Include(r => r.Bookings.Where(b => !b.Cancelled && b.StartTime >= startDate && b.EndTime <= endDate))
                    .ToListAsync();

                var stats = resources
                    .Where(r => r.Bookings.Any())
                    .Select(r => new ResourcePopularityStats
                    {
                        ResourceName = r.Name,
                        BookingCount = r.Bookings.Count,
                        TotalHours = Math.Round(r.Bookings.Sum(b => (double)(b.EndTime - b.StartTime).TotalHours), 2)
                    })
                    .OrderByDescending(s => s.BookingCount)
                    .ToList();

                var totalPossibleHours = CalculateBusinessHoursClipped(startDate, endDate);
                foreach (var stat in stats)
                {
                    stat.UtilizationPercentage = totalPossibleHours > 0 ? Math.Round((stat.TotalHours / totalPossibleHours) * 100, 2) : 0;
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
                var bookings = await _db.Bookings.AsNoTracking()
                    .Where(b => b.ResourceId == resourceId && !b.Cancelled && b.StartTime < endDate && b.EndTime > startDate)
                    .ToListAsync();

                double minutes = 0;
                foreach (var b in bookings)
                {
                    var s = b.StartTime < startDate ? startDate : b.StartTime;
                    var e = b.EndTime > endDate ? endDate : b.EndTime;
                    if (e <= s) continue;
                    minutes += CalculateBookedMinutesWithinBusinessHours(s, e);
                }

                var totalBookedHours = minutes / 60.0;
                var totalPossibleHours = CalculateBusinessHoursClipped(startDate, endDate);
                var utilization = totalPossibleHours > 0 ? (totalBookedHours / totalPossibleHours) * 100 : 0;
                return utilization;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating utilization percentage for resource {ResourceId}: {Message}", resourceId, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Calculate total business hours between two dates (Mon–Fri, 08:00–18:00). Returns hours.
        /// </summary>
        private double CalculateBusinessHoursClipped(DateTime start, DateTime end)
        {
            if (end <= start) return 0;
            double minutes = 0;
            var current = start.Date;
            var lastDate = end.Date;

            while (current <= lastDate)
            {
                if (BusinessDays.Contains((int)current.DayOfWeek))
                {
                    var dayStart = current.AddHours(BusinessStartHour);
                    var dayEnd = current.AddHours(BusinessEndHour);

                    var windowStart = start > dayStart ? start : dayStart;
                    var windowEnd = end < dayEnd ? end : dayEnd;

                    if (windowEnd > windowStart)
                        minutes += (windowEnd - windowStart).TotalMinutes;
                }
                current = current.AddDays(1);
            }
            return Math.Round(minutes / 60.0, 2);
        }

        /// <summary>
        /// Calculate minutes of an interval that fall within business hours (Mon–Fri, 08:00–18:00).
        /// </summary>
        private double CalculateBookedMinutesWithinBusinessHours(DateTime start, DateTime end)
        {
            if (end <= start) return 0;
            double minutes = 0;
            var current = start.Date;
            var lastDate = end.Date;

            while (current <= lastDate)
            {
                if (BusinessDays.Contains((int)current.DayOfWeek))
                {
                    var dayStart = current.AddHours(BusinessStartHour);
                    var dayEnd = current.AddHours(BusinessEndHour);

                    var windowStart = start > dayStart ? start : dayStart;
                    var windowEnd = end < dayEnd ? end : dayEnd;

                    if (windowEnd > windowStart)
                        minutes += (windowEnd - windowStart).TotalMinutes;
                }
                current = current.AddDays(1);
            }
            return minutes;
        }
    }
}