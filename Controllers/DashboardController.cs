using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;

namespace ResourceBooking.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICalculationService _calculationService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext db, 
            UserManager<ApplicationUser> userManager,
            ICalculationService calculationService,
            ILogger<DashboardController> logger)
        {
            _db = db;
            _userManager = userManager;
            _calculationService = calculationService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();
            
            try
            {
                var userId = _userManager.GetUserId(User);
                var today = DateTime.Today;

                _logger.LogInformation("Loading dashboard for user {UserId}", userId);

                // Get dashboard statistics with detailed logging
                _logger.LogInformation("Fetching dashboard statistics...");
                var dashboardStats = await _calculationService.GetDashboardStatsAsync();
                viewModel.DashboardStats = dashboardStats;

                // Get user's bookings for today
                _logger.LogInformation("Fetching user's bookings for today...");
                var userTodayBookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Where(b => b.UserId == userId && 
                               !b.Cancelled && 
                               b.StartTime.Date == today)
                    .OrderBy(b => b.StartTime)
                    .ToListAsync();
                viewModel.UserTodayBookings = userTodayBookings;

                // Get user's upcoming bookings (next 7 days)
                _logger.LogInformation("Fetching user's upcoming bookings...");
                var userUpcomingBookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Where(b => b.UserId == userId && 
                               !b.Cancelled && 
                               b.StartTime > DateTime.Now &&
                               b.StartTime <= DateTime.Now.AddDays(7))
                    .OrderBy(b => b.StartTime)
                    .Take(5)
                    .ToListAsync();
                viewModel.UserUpcomingBookings = userUpcomingBookings;

                // Get popular resources this month
                _logger.LogInformation("Fetching popular resources...");
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1); // exclusive upper bound
                var popularResources = await _calculationService.GetResourcePopularityStatsAsync(startOfMonth, today.AddDays(1));
                viewModel.PopularResources = popularResources.Take(5).ToList();

                // Get user stats for this month (full month window so cancellations of future bookings count)
                _logger.LogInformation("Fetching user statistics (month-wide)...");
                var userStats = await _calculationService.GetUserBookingStatsAsync(userId, startOfMonth, endOfMonth);
                viewModel.UserStats = userStats;

                _logger.LogInformation("Dashboard data loaded successfully. Resources: {ResourceCount}, Today's Bookings: {TodayBookings}", 
                    dashboardStats.TotalResources, dashboardStats.TotalBookingsToday);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Unable to load dashboard data. Please try again.";
                
                // Return view with empty data instead of failing completely
                return View(viewModel);
            }
        }
    }

    // ViewModel for the dashboard
    public class DashboardViewModel
    {
        public DashboardStats DashboardStats { get; set; } = new();
        public List<Booking> UserTodayBookings { get; set; } = new();
        public List<Booking> UserUpcomingBookings { get; set; } = new();
        public List<ResourcePopularityStats> PopularResources { get; set; } = new();
        public UserBookingStats UserStats { get; set; } = new();
    }
}