using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Services;

namespace ResourceBooking.Controllers
{
    public class DiagnosticsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICalculationService _calculationService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(ApplicationDbContext db, ICalculationService calculationService, ILogger<DiagnosticsController> logger)
        {
            _db = db;
            _calculationService = calculationService;
            _logger = logger;
        }

        public async Task<IActionResult> TestReports()
        {
            var results = new List<string>();

            try
            {
                // Test 1: Basic database connectivity
                results.Add("=== DATABASE CONNECTIVITY TEST ===");
                var canConnect = await _db.Database.CanConnectAsync();
                results.Add($"Can connect to database: {canConnect}");

                // Test 2: Count basic entities
                results.Add("\n=== BASIC COUNTS ===");
                var resourceCount = await _db.Resources.CountAsync();
                var userCount = await _db.Users.CountAsync();
                var bookingCount = await _db.Bookings.CountAsync();
                results.Add($"Resources: {resourceCount}");
                results.Add($"Users: {userCount}");
                results.Add($"Bookings: {bookingCount}");

                // Test 3: Test dashboard stats calculation
                results.Add("\n=== DASHBOARD STATS TEST ===");
                try
                {
                    var dashboardStats = await _calculationService.GetDashboardStatsAsync();
                    results.Add($"Dashboard Stats Success:");
                    results.Add($"  Total Resources: {dashboardStats.TotalResources}");
                    results.Add($"  Available Resources: {dashboardStats.AvailableResources}");
                    results.Add($"  Today's Bookings: {dashboardStats.TotalBookingsToday}");
                    results.Add($"  This Month's Bookings: {dashboardStats.TotalBookingsThisMonth}");
                    results.Add($"  Utilization: {dashboardStats.AverageUtilizationPercentage}%");
                    results.Add($"  Active Users: {dashboardStats.ActiveUsers}");
                }
                catch (Exception ex)
                {
                    results.Add($"Dashboard Stats ERROR: {ex.Message}");
                    results.Add($"Stack Trace: {ex.StackTrace}");
                }

                // Test 4: Test individual queries that might be failing
                results.Add("\n=== INDIVIDUAL QUERY TESTS ===");
                
                // Test bookings with date filters
                var today = DateTime.Today;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfToday = today.AddDays(1);

                try
                {
                    var todayBookings = await _db.Bookings
                        .Where(b => !b.Cancelled && b.StartTime >= today && b.StartTime < endOfToday)
                        .CountAsync();
                    results.Add($"Today's bookings query: SUCCESS ({todayBookings} bookings)");
                }
                catch (Exception ex)
                {
                    results.Add($"Today's bookings query: ERROR - {ex.Message}");
                }

                try
                {
                    var monthBookings = await _db.Bookings
                        .Where(b => !b.Cancelled && b.StartTime >= startOfMonth && b.StartTime < endOfToday)
                        .CountAsync();
                    results.Add($"Month's bookings query: SUCCESS ({monthBookings} bookings)");
                }
                catch (Exception ex)
                {
                    results.Add($"Month's bookings query: ERROR - {ex.Message}");
                }

                // Test 5: Test resource popularity calculation
                try
                {
                    var resourceStats = await _calculationService.GetResourcePopularityStatsAsync(startOfMonth, endOfToday);
                    results.Add($"Resource popularity query: SUCCESS ({resourceStats.Count} resources)");
                }
                catch (Exception ex)
                {
                    results.Add($"Resource popularity query: ERROR - {ex.Message}");
                }

                // Test 6: Sample data verification
                results.Add("\n=== SAMPLE DATA VERIFICATION ===");
                var sampleResources = await _db.Resources.Take(3).ToListAsync();
                foreach (var resource in sampleResources)
                {
                    results.Add($"Resource: {resource.Name} (Available: {resource.IsAvailable})");
                }

                var sampleUsers = await _db.Users.Take(3).ToListAsync();
                foreach (var user in sampleUsers)
                {
                    results.Add($"User: {user.FirstName} {user.LastName} ({user.Email})");
                }

                var sampleBookings = await _db.Bookings.Take(3).ToListAsync();
                foreach (var booking in sampleBookings)
                {
                    results.Add($"Booking: Resource ID {booking.ResourceId}, Start: {booking.StartTime}, Cancelled: {booking.Cancelled}");
                }

            }
            catch (Exception ex)
            {
                results.Add($"GENERAL ERROR: {ex.Message}");
                results.Add($"Stack Trace: {ex.StackTrace}");
            }

            // Return results as plain text
            return Content(string.Join("\n", results), "text/plain");
        }
    }
}