using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;
using ResourceBooking.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ResourceBooking.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookingsController> _logger;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;

        public BookingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<BookingsController> logger, IEmailService emailService, INotificationService notificationService)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        // NOTE: Index/Create/Edit are served by Razor Pages under /Pages/Bookings

        // POST: Bookings/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Resource)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);
                
            if (booking == null) return NotFound();
            
            var currentUserId = _userManager.GetUserId(User);
            if (booking.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "You can only cancel your own bookings.";
                return Redirect("/Bookings");
            }
            
            try
            {
                booking.Cancelled = true;
                await _db.SaveChangesAsync();

                // Send cancellation email
                try
                {
                    await _emailService.SendBookingCancellationAsync(booking);
                    _logger.LogInformation("Booking cancellation email sent for booking {BookingId}", booking.Id);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send booking cancellation email for booking {BookingId}", booking.Id);
                }
                
                TempData["SuccessMessage"] = "Booking cancelled successfully! Cancellation email sent.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking: {Message}", ex.Message);
                TempData["ErrorMessage"] = "An error occurred while cancelling the booking.";
            }
            
            return Redirect("/Bookings");
        }

        // GET: Bookings/Calendar
        public async Task<IActionResult> Calendar()
        {
            ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
            return View();
        }

        // API endpoint for calendar events
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(int? resourceId, DateTime? start, DateTime? end)
        {
            var query = _db.Bookings
                .Include(b => b.Resource)
                .Include(b => b.User)
                .Where(b => !b.Cancelled)
                .AsQueryable();

            if (resourceId.HasValue && resourceId > 0)
                query = query.Where(b => b.ResourceId == resourceId.Value);

            if (start.HasValue)
                query = query.Where(b => b.EndTime >= start.Value);

            if (end.HasValue)
                query = query.Where(b => b.StartTime <= end.Value);

            var bookings = await query.ToListAsync();

            var events = bookings.Select(b => new
            {
                id = b.Id,
                title = $"{b.Resource.Name} - {b.Purpose}",
                start = b.LocalStartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = b.LocalEndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                backgroundColor = GetColorForResource(b.ResourceId),
                borderColor = GetColorForResource(b.ResourceId),
                extendedProps = new
                {
                    resourceName = b.Resource.Name,
                    bookedBy = b.User.FullName,
                    purpose = b.Purpose,
                    canEdit = b.UserId == _userManager.GetUserId(User)
                }
            });

            return Json(events);
        }

        private string GetColorForResource(int resourceId)
        {
            var colors = new[] { "#007bff", "#28a745", "#dc3545", "#ffc107", "#6f42c1", "#e83e8c", "#fd7e14", "#20c997" };
            return colors[resourceId % colors.Length];
        }

        [HttpGet]
        public async Task<IActionResult> Export(int? resourceId, DateTime? date, string? search)
        {
            var query = _db.Bookings
                .Include(b => b.Resource)
                .Include(b => b.User)
                .Where(b => !b.Cancelled)
                .AsNoTracking()
                .AsQueryable();

            if (resourceId.HasValue)
                query = query.Where(b => b.ResourceId == resourceId.Value);
            if (date.HasValue)
                query = query.Where(b => b.StartTime.Date == date.Value.Date);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(b => b.Resource.Name.Contains(search) || b.Purpose.Contains(search) || b.User.FirstName.Contains(search) || b.User.LastName.Contains(search));

            var data = await query.OrderBy(b => b.StartTime).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,Resource,Start,End,BookedBy,Purpose,Status");
            foreach (var b in data)
            {
                var status = b.Cancelled ? "Cancelled" : (b.EndTime < DateTime.UtcNow ? "Completed" : (b.StartTime <= DateTime.UtcNow && b.EndTime > DateTime.UtcNow ? "In Progress" : "Upcoming"));
                string Csv(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
                sb.AppendLine(string.Join(',', new[]
                {
                    b.Id.ToString(),
                    Csv(b.Resource?.Name ?? ""),
                    Csv(b.LocalStartTime.ToString("yyyy-MM-dd HH:mm")),
                    Csv(b.LocalEndTime.ToString("yyyy-MM-dd HH:mm")),
                    Csv(b.BookedByName),
                    Csv(b.Purpose),
                    Csv(status)
                }));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"bookings_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // Download booking as ICS
        [HttpGet]
        public async Task<IActionResult> Ics(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Resource)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            var ics = BuildIcs(booking);
            var bytes = Encoding.UTF8.GetBytes(ics);
            var fileName = $"booking_{booking.Id}_{booking.LocalStartTime:yyyyMMdd_HHmm}.ics";
            return File(bytes, "text/calendar; charset=utf-8", fileName);
        }

        private static string BuildIcs(Booking booking)
        {
            string Escape(string? s)
            {
                if (string.IsNullOrEmpty(s)) return string.Empty;
                return s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
            }

            string dt(DateTime utc) => utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("PRODID:-//ADS Corp//Resource Booking//EN");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{booking.Id}@adscorp.com");
            sb.AppendLine($"DTSTAMP:{dt(DateTime.UtcNow)}");
            sb.AppendLine($"DTSTART:{dt(booking.StartTime)}");
            sb.AppendLine($"DTEND:{dt(booking.EndTime)}");
            sb.AppendLine($"SUMMARY:{Escape(booking.Resource?.Name)} - {Escape(booking.Purpose)}");
            if (!string.IsNullOrWhiteSpace(booking.Resource?.Location))
                sb.AppendLine($"LOCATION:{Escape(booking.Resource.Location)}");
            var desc = $"Booked by: {booking.BookedByName}\\nResource: {booking.Resource?.Name}\\nPurpose: {booking.Purpose}";
            sb.AppendLine($"DESCRIPTION:{Escape(desc)}");
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");
            return sb.ToString();
        }
    }
}