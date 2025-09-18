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

        // Added optional quick range + custom date parameters
        public async Task<IActionResult> Index(string? range, DateTime? start, DateTime? end)
        {
            try
            {
                // Resolve date range
                var today = DateTime.Today;
                DateTime rangeStart;
                DateTime rangeEndExclusive; // exclusive end boundary
                string quickRange = range ?? "this-month";

                switch (quickRange)
                {
                    case "last-month":
                        var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
                        rangeStart = firstOfThisMonth.AddMonths(-1);
                        rangeEndExclusive = firstOfThisMonth; // up to before this month
                        break;
                    case "last-90":
                        rangeStart = today.AddDays(-89); // include today -> 90 days window
                        rangeEndExclusive = today.AddDays(1);
                        break;
                    case "custom":
                        if (start.HasValue && end.HasValue && start.Value.Date <= end.Value.Date)
                        {
                            rangeStart = start.Value.Date;
                            rangeEndExclusive = end.Value.Date.AddDays(1);
                        }
                        else
                        {
                            // fallback to this month if invalid custom
                            rangeStart = new DateTime(today.Year, today.Month, 1);
                            rangeEndExclusive = rangeStart.AddMonths(1);
                            quickRange = "this-month";
                        }
                        break;
                    case "this-month":
                    default:
                        rangeStart = new DateTime(today.Year, today.Month, 1);
                        rangeEndExclusive = rangeStart.AddMonths(1);
                        quickRange = "this-month";
                        break;
                }

                bool isCustom = quickRange == "custom";

                _logger.LogInformation("Reports range resolved. QuickRange={Quick} Start={Start} EndExclusive={End}", quickRange, rangeStart, rangeEndExclusive);

                var dashboardStats = await _calculationService.GetDashboardStatsAsync() ?? new DashboardStats();

                // Monthly comparison (still month based irrespective of view range for now)
                var thisMonthStart = new DateTime(today.Year, today.Month, 1);
                var thisMonthEnd = thisMonthStart.AddMonths(1);
                var lastMonthStart = thisMonthStart.AddMonths(-1);

                var thisMonthBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && b.StartTime >= thisMonthStart && b.StartTime < thisMonthEnd)
                    .CountAsync();

                var lastMonthBookings = await _db.Bookings
                    .Where(b => !b.Cancelled && b.StartTime >= lastMonthStart && b.StartTime < thisMonthStart)
                    .CountAsync();

                // Pull raw booking rows for selected range (SQLite friendly)
                var rangeBookingsRaw = await _db.Bookings
                    .AsNoTracking()
                    .Where(b => !b.Cancelled && b.StartTime >= rangeStart && b.StartTime < rangeEndExclusive)
                    .Select(b => new { b.ResourceId, b.UserId, b.StartTime, b.EndTime })
                    .ToListAsync();

                // Resource aggregates
                var resourceAgg = rangeBookingsRaw
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

                var businessHoursThisPeriod = CalculateBusinessHours(rangeStart, rangeEndExclusive);
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

                // User aggregates
                var userAgg = rangeBookingsRaw
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
                    ReportDate = today,
                    SelectedQuickRange = quickRange,
                    RangeStart = rangeStart,
                    RangeEnd = rangeEndExclusive.AddDays(-1), // inclusive end for display
                    IsCustom = isCustom
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reports: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Unable to load reports data. Please try again.";

                var fallbackModel = new ReportsViewModel
                {
                    DashboardStats = new DashboardStats(),
                    SelectedQuickRange = "this-month",
                    RangeStart = DateTime.Today,
                    RangeEnd = DateTime.Today,
                    IsCustom = false
                };
                return View(fallbackModel);
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

        // New range-related properties
        public string SelectedQuickRange { get; set; } = "this-month";
        public DateTime RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public bool IsCustom { get; set; }
        public string RangeLabel => SelectedQuickRange switch
        {
            "this-month" => "This Month",
            "last-month" => "Last Month",
            "last-90" => "Last 90 Days",
            "custom" => $"Custom: {RangeStart:dd MMM yyyy} - {RangeEnd:dd MMM yyyy}",
            _ => "This Month"
        };
    }

    public class UserActivityReport
    {
        public string UserName { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public double TotalHours { get; set; }
    }
}