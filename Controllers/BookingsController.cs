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

        // GET: Bookings
        public async Task<IActionResult> Index(int? resourceId, DateTime? date, string search, int page = 1, int pageSize = 15)
        {
            var query = _db.Bookings
                .Include(b => b.Resource)
                .Include(b => b.User)
                .Where(b => !b.Cancelled)
                .AsQueryable();

            if (resourceId.HasValue) 
                query = query.Where(b => b.ResourceId == resourceId.Value);
            
            if (date.HasValue) 
                query = query.Where(b => b.StartTime.Date == date.Value.Date);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => 
                    b.Resource.Name.Contains(search) || 
                    b.Purpose.Contains(search) ||
                    b.User.FirstName.Contains(search) ||
                    b.User.LastName.Contains(search));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();
            
            // Apply pagination
            var bookings = await query
                .OrderBy(b => b.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Pass resources for filtering dropdown
            ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
            ViewBag.CurrentResourceId = resourceId;
            ViewBag.CurrentDate = date;
            ViewBag.CurrentSearch = search;
            
            // Pagination data
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < ViewBag.TotalPages;
            
            return View(bookings);
        }

        // GET: Bookings/Create
        public async Task<IActionResult> Create(int? resourceId)
        {
            ViewBag.Resources = await _db.Resources.Where(r => r.IsAvailable).OrderBy(r => r.Name).ToListAsync();
            
            var viewModel = new CreateBookingViewModel();
            if (resourceId.HasValue)
            {
                viewModel.ResourceId = resourceId.Value;
            }
            
            return View(viewModel);
        }

        // POST: Bookings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingViewModel viewModel)
        {
            _logger.LogInformation("Creating booking - ResourceId: {ResourceId}, StartTime: {StartTime}, EndTime: {EndTime}", viewModel.ResourceId, viewModel.StartTime, viewModel.EndTime);

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) ModelState.AddModelError("", "User not found. Please log in again.");
            if (viewModel.EndTime <= viewModel.StartTime) ModelState.AddModelError(nameof(viewModel.EndTime), "End time must be after start time.");

            if (viewModel.IsRecurring)
            {
                var validationErrors = new List<string>();
                if (!viewModel.IsValid(out validationErrors)) foreach (var e in validationErrors) ModelState.AddModelError("", e);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Resources = await _db.Resources.Where(r => r.IsAvailable).OrderBy(r => r.Name).ToListAsync();
                return View(viewModel);
            }

            var booking = viewModel.ToBooking(userId); // UTC times

            try
            {
                if (!booking.IsRecurring)
                {
                    var candidates = await GetConflictCandidatesAsync(booking.ResourceId, booking.StartTime, booking.EndTime);
                    var conflicts = candidates.Where(b => IntervalsOverlap(booking.StartTime, booking.EndTime, b.StartTime, b.EndTime)).ToList();

                    if (conflicts.Any())
                    {
                        var details = string.Join("; ", conflicts.Select(c => $"#{c.Id} {TimeZoneHelper.ConvertToLocal(c.StartTime):MMM dd HH:mm} - {TimeZoneHelper.ConvertToLocal(c.EndTime):HH:mm} ({c.Purpose})"));
                        _logger.LogWarning("Conflict detected for resource {Res}: {Details}", booking.ResourceId, details);
                        ModelState.AddModelError("", "This resource is already booked during the requested time. Please choose another slot or resource, or adjust your times.");
                        ModelState.AddModelError("", $"Conflicts found: {details}");
                        ViewBag.Resources = await _db.Resources.Where(r => r.IsAvailable).OrderBy(r => r.Name).ToListAsync();
                        return View(viewModel);
                    }
                }
                else
                {
                    var recurringService = HttpContext.RequestServices.GetRequiredService<IRecurringBookingService>();
                    var all = await recurringService.GenerateRecurringBookingsAsync(booking);

                    foreach (var item in all)
                    {
                        var cands = await GetConflictCandidatesAsync(item.ResourceId, item.StartTime, item.EndTime);
                        if (cands.Any(b => IntervalsOverlap(item.StartTime, item.EndTime, b.StartTime, b.EndTime)))
                        {
                            ModelState.AddModelError("", "One or more recurring bookings conflict with existing bookings. Please choose different times or dates.");
                            ViewBag.Resources = await _db.Resources.Where(r => r.IsAvailable).OrderBy(r => r.Name).ToListAsync();
                            return View(viewModel);
                        }
                    }

                    // Save recurring series
                    var parent = all.First();
                    _db.Bookings.Add(parent);
                    await _db.SaveChangesAsync();
                    foreach (var child in all.Skip(1)) { child.ParentBookingId = parent.Id; _db.Bookings.Add(child); }
                    await _db.SaveChangesAsync();

                    await _db.Entry(parent).Reference(b => b.Resource).LoadAsync();
                    await _db.Entry(parent).Reference(b => b.User).LoadAsync();
                    try { await _emailService.SendBookingConfirmationAsync(parent); } catch { }
                    TempData["SuccessMessage"] = $"Recurring booking created successfully! {all.Count} bookings were created. Confirmation email sent.";
                    return RedirectToAction(nameof(Index));
                }

                // Single booking save
                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                await _db.Entry(booking).Reference(b => b.Resource).LoadAsync();
                await _db.Entry(booking).Reference(b => b.User).LoadAsync();
                try { await _emailService.SendBookingConfirmationAsync(booking); } catch { }
                TempData["SuccessMessage"] = "Booking created successfully! Confirmation email sent.";
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking: {Message}", ex.Message);
                ModelState.AddModelError("", "An error occurred while creating the booking: " + ex.Message);
                ViewBag.Resources = await _db.Resources.Where(r => r.IsAvailable).OrderBy(r => r.Name).ToListAsync();
                return View(viewModel);
            }
        }

        // GET: Bookings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            
            var booking = await _db.Bookings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);
                
            if (booking == null) return NotFound();
            
            // Only allow users to edit their own bookings
            var currentUserId = _userManager.GetUserId(User);
            if (booking.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "You can only edit your own bookings.";
                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
            
            // Convert Booking to ViewModel
            var viewModel = EditBookingViewModel.FromBooking(booking);
            return View(viewModel);
        }

        // POST: Bookings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditBookingViewModel viewModel)
        {
            if (id != viewModel.Id) return NotFound();
            
            _logger.LogInformation("Editing booking ID: {BookingId} - ResourceId: {ResourceId}, StartTime: {StartTime}, EndTime: {EndTime}, Purpose: {Purpose}", 
                viewModel.Id, viewModel.ResourceId, viewModel.StartTime, viewModel.EndTime, viewModel.Purpose);

            // Verify user owns this booking
            var currentUserId = _userManager.GetUserId(User);
            if (viewModel.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "You can only edit your own bookings.";
                return RedirectToAction(nameof(Index));
            }

            // Additional validation
            if (viewModel.EndTime <= viewModel.StartTime)
            {
                ModelState.AddModelError(nameof(viewModel.EndTime), "End time must be after start time.");
            }

            // Log model state for debugging
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState)
                {
                    _logger.LogWarning("Model validation error - Key: {Key}, Errors: {Errors}", 
                        error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                }
                
                ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
                return View(viewModel);
            }

            // Convert proposed times to UTC for proper conflict check
            var proposedStartUtc = TimeZoneHelper.ConvertToUtc(viewModel.StartTime);
            var proposedEndUtc = TimeZoneHelper.ConvertToUtc(viewModel.EndTime);

            var candidates = await GetConflictCandidatesAsync(viewModel.ResourceId, proposedStartUtc, proposedEndUtc, excludeBookingId: viewModel.Id);
            if (candidates.Any(b => IntervalsOverlap(proposedStartUtc, proposedEndUtc, b.StartTime, b.EndTime)))
            {
                ModelState.AddModelError("", "This resource is already booked during the requested time.");
                ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
                return View(viewModel);
            }

            try
            {
                var existingBooking = await _db.Bookings
                    .Include(b => b.Resource)
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == id);
                
                if (existingBooking == null) return NotFound();
                var wasCancelled = existingBooking.Cancelled;

                // Update the booking with ViewModel data
                viewModel.UpdateBooking(existingBooking);
                
                _logger.LogInformation("Attempting to update booking in database");
                await _db.SaveChangesAsync();
                
                // Send cancellation email if booking was just cancelled
                if (!wasCancelled && existingBooking.Cancelled)
                {
                    try
                    {
                        await _emailService.SendBookingCancellationAsync(existingBooking);
                        _logger.LogInformation("Booking cancellation email sent for booking {BookingId}", existingBooking.Id);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send booking cancellation email for booking {BookingId}", existingBooking.Id);
                    }
                }
                
                _logger.LogInformation("Booking updated successfully");
                TempData["SuccessMessage"] = existingBooking.Cancelled ? "Booking cancelled successfully! Cancellation email sent." : "Booking updated successfully!";

                // In-app notification
                var notifMessage = existingBooking.Cancelled ? "Booking cancelled" : "Booking updated";
                try { await _notificationService.CreateSystemNotificationAsync(existingBooking.UserId, notifMessage, $"Your booking for {existingBooking.Resource.Name} has been " + (existingBooking.Cancelled ? "cancelled" : "updated") + ".", existingBooking.Id); } catch { }
                
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating booking: {Message}", ex.Message);
                if (!_db.Bookings.Any(e => e.Id == viewModel.Id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking: {Message}", ex.Message);
                ModelState.AddModelError("", "An error occurred while updating the booking: " + ex.Message);
                ViewBag.Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
                return View(viewModel);
            }
        }

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
                return RedirectToAction(nameof(Index));
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
            
            return RedirectToAction(nameof(Index));
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
                start = b.LocalStartTime.ToString("yyyy-MM-ddTHH:mm:ss"), // Use local time for calendar
                end = b.LocalEndTime.ToString("yyyy-MM-ddTHH:mm:ss"),     // Use local time for calendar
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

        private static bool IntervalsOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        {
            return aStart < bEnd && bStart < aEnd; // end-exclusive
        }

        private async Task<List<Booking>> GetConflictCandidatesAsync(int resourceId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null)
        {
            var windowStart = startUtc.Date.AddDays(-1);
            var windowEnd = endUtc.Date.AddDays(1);
            var q = _db.Bookings
                .Where(b => b.ResourceId == resourceId && !b.Cancelled)
                .Where(b => b.EndTime > windowStart && b.StartTime < windowEnd);
            if (excludeBookingId.HasValue) q = q.Where(b => b.Id != excludeBookingId.Value);
            return await q.ToListAsync();
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
                // CSV-safe values
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