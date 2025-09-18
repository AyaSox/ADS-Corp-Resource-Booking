using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceBooking.Services;
using ResourceBooking.Data;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Models;

namespace ResourceBooking.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICalculationService _calculationService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ApplicationDbContext db, ICalculationService calculationService, ILogger<ReportsController> logger)
        {
            _db = db;
            _calculationService = calculationService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("=== REPORTS CONTROLLER - Starting Data Collection ===");
                var today = DateTime.Today;
                var endOfToday = today.AddDays(1);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfLastMonth = startOfMonth.AddMonths(-1);

                var dashboardStats = await _calculationService.GetDashboardStatsAsync() ?? new DashboardStats();

                // Monthly comparison
                var thisMonthBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.StartTime < endOfToday)
                    .CountAsync();

                var lastMonthBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && b.StartTime >= startOfLastMonth && b.StartTime < startOfMonth)
                    .CountAsync();

                // Pull raw booking rows once, then aggregate in-memory to avoid provider translation issues (SQLite limitation)
                var monthBookingsRaw = await _db.Bookings
                    .AsNoTracking()
                    .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.StartTime < endOfToday)
                    .Select(b => new { b.ResourceId, b.UserId, b.StartTime, b.EndTime })
                    .ToListAsync();

                // Resource aggregates (in memory)
                var resourceAgg = monthBookingsRaw
                    .GroupBy(b => b.ResourceId)
                    .Select(g => new
                    {
                        ResourceId = g.Key,
                        BookingCount = g.Count(),
                        TotalMinutes = g.Sum(x => (x.EndTime - x.StartTime).TotalMinutes)
                    })
                    .OrderByDescending(x => x.BookingCount)
                    .ToList();

                var resourceIds = resourceAgg.Select(x => x.ResourceId).ToList();
                var resourceNames = await _db.Resources
                    .Where(r => resourceIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id, r => r.Name);

                var businessHoursThisPeriod = CalculateBusinessHours(startOfMonth, endOfToday);
                var maxBookings = resourceAgg.Any() ? resourceAgg.Max(x => x.BookingCount) : 0;
                var maxHours = resourceAgg.Any() ? resourceAgg.Max(x => x.TotalMinutes) / 60.0 : 0.0;
                const double wBookings = 0.6;
                const double wHours = 0.4;

                var popularResources = resourceAgg
                    .Select(x =>
                    {
                        var hours = x.TotalMinutes / 60.0;
                        var utilizationPct = businessHoursThisPeriod > 0 ? Math.Round((hours / businessHoursThisPeriod) * 100, 2) : 0;
                        var scoreBookings = maxBookings > 0 ? (double)x.BookingCount / maxBookings : 0;
                        var scoreHours = maxHours > 0 ? hours / maxHours : 0;
                        var score = (scoreBookings * wBookings) + (scoreHours * wHours);
                        var stars = Math.Round(score * 5, 1);
                        return new ResourcePopularityStats
                        {
                            ResourceName = resourceNames.TryGetValue(x.ResourceId, out var name) ? name : $"Resource #{x.ResourceId}",
                            BookingCount = x.BookingCount,
                            TotalHours = Math.Round(hours, 2),
                            UtilizationPercentage = utilizationPct,
                            PopularityStars = stars
                        };
                    })
                    .OrderByDescending(s => s.PopularityStars)
                    .ThenByDescending(s => s.BookingCount)
                    .ToList();

                _logger.LogInformation("Popular resources computed: {Count}", popularResources.Count);

                // User aggregates (in memory)
                var userAgg = monthBookingsRaw
                    .GroupBy(b => b.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        BookingCount = g.Count(),
                        TotalMinutes = g.Sum(x => (x.EndTime - x.StartTime).TotalMinutes)
                    })
                    .OrderByDescending(x => x.BookingCount)
                    .Take(10)
                    .ToList();

                var userIds = userAgg.Select(x => x.UserId).Where(id => id != null).ToList();
                var userNames = await _db.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => ($"{u.FirstName} {u.LastName}").Trim());

                var userActivity = userAgg
                    .Select(x => new UserActivityReport
                    {
                        UserName = x.UserId != null && userNames.TryGetValue(x.UserId, out var n) ? n : "Unknown",
                        BookingCount = x.BookingCount,
                        TotalHours = Math.Round(x.TotalMinutes / 60.0, 2)
                    })
                    .ToList();

                var model = new ReportsViewModel
                {
                    DashboardStats = dashboardStats,
                    PopularResources = popularResources,
                    ThisMonthBookings = thisMonthBookings,
                    LastMonthBookings = lastMonthBookings,
                    UserActivity = userActivity,
                    ReportDate = today
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reports: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Unable to load reports data. Please try again.";

                var debugModel = new ReportsViewModel
                {
                    DashboardStats = new DashboardStats
                    {
                        TotalResources = await _db.Resources.CountAsync(),
                        AvailableResources = await _db.Resources.CountAsync(r => r.IsAvailable),
                        UnavailableResources = await _db.Resources.CountAsync(r => !r.IsAvailable),
                        TotalBookingsThisMonth = await _db.Bookings.CountAsync()
                    },
                    PopularResources = new List<ResourcePopularityStats>(),
                    ThisMonthBookings = await _db.Bookings.CountAsync(),
                    LastMonthBookings = 0,
                    UserActivity = new List<UserActivityReport>(),
                    ReportDate = DateTime.Today
                };

                return View(debugModel);
            }
        }

        private static double CalculateBusinessHours(DateTime startDate, DateTime endDate)
        {
            const int startHour = 8;
            const int endHour = 18;
            const int hoursPerDay = endHour - startHour;
            var businessDays = new HashSet<int> { 1, 2, 3, 4, 5 };

            double total = 0;
            var current = startDate.Date;
            var last = endDate.Date;
            while (current < last)
            {
                if (businessDays.Contains((int)current.DayOfWeek))
                    total += hoursPerDay;
                current = current.AddDays(1);
            }
            return total;
        }
    }

    public class ReportsViewModel
    {
        public DashboardStats DashboardStats { get; set; } = new();
        public List<ResourcePopularityStats> PopularResources { get; set; } = new();
        public int ThisMonthBookings { get; set; }
        public int LastMonthBookings { get; set; }
        public List<UserActivityReport> UserActivity { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public double MonthlyGrowthPercentage => LastMonthBookings == 0 ? (ThisMonthBookings > 0 ? 100 : 0) : Math.Round(((double)(ThisMonthBookings - LastMonthBookings) / LastMonthBookings) * 100, 1);
    }

    public class UserActivityReport
    {
        public string UserName { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public double TotalHours { get; set; }
    }
}