using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Helpers;
using ResourceBooking.Models;
using ResourceBooking.Services;

namespace ResourceBooking.Pages.Bookings;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public CreateModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailService emailService)
    {
        _db = db;
        _userManager = userManager;
        _emailService = emailService;
    }

    public List<Resource> Resources { get; set; } = new();

    [BindProperty]
    public CreateBookingViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? resourceId)
    {
        // Show all resources so users can see options even if some are unavailable
        Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
        if (resourceId.HasValue) Input.ResourceId = resourceId.Value;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Repopulate for redisplay on validation errors
        Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) { ModelState.AddModelError("", "User not found. Please log in again."); }
        if (Input.EndTime <= Input.StartTime) { ModelState.AddModelError(nameof(Input.EndTime), "End time must be after start time."); }

        if (Input.IsRecurring)
        {
            var errors = new List<string>();
            if (!Input.IsValid(out errors)) foreach (var e in errors) ModelState.AddModelError("", e);
        }

        if (!ModelState.IsValid) return Page();

        var booking = Input.ToBooking(userId!); // UTC conversion inside

        try
        {
            if (!booking.IsRecurring)
            {
                var windowStart = booking.StartTime.Date.AddDays(-1);
                var windowEnd = booking.EndTime.Date.AddDays(1);
                var conflicts = await _db.Bookings.Where(b => b.ResourceId == booking.ResourceId && !b.Cancelled && b.EndTime > windowStart && b.StartTime < windowEnd).ToListAsync();
                if (conflicts.Any(b => booking.StartTime < b.EndTime && b.StartTime < booking.EndTime))
                {
                    ModelState.AddModelError("", "This resource is already booked during the requested time.");
                    return Page();
                }
            }
            else
            {
                var recurringService = HttpContext.RequestServices.GetRequiredService<IRecurringBookingService>();
                var all = await recurringService.GenerateRecurringBookingsAsync(booking, HttpContext.RequestAborted);
                foreach (var item in all)
                {
                    var windowStart = item.StartTime.Date.AddDays(-1);
                    var windowEnd = item.EndTime.Date.AddDays(1);
                    var cands = await _db.Bookings.Where(b => b.ResourceId == item.ResourceId && !b.Cancelled && b.EndTime > windowStart && b.StartTime < windowEnd).ToListAsync();
                    if (cands.Any(b => item.StartTime < b.EndTime && b.StartTime < item.EndTime))
                    {
                        ModelState.AddModelError("", "One or more recurring bookings conflict with existing bookings.");
                        return Page();
                    }
                }

                // Save series
                var parent = all.First();
                _db.Bookings.Add(parent);
                await _db.SaveChangesAsync();
                foreach (var child in all.Skip(1)) { child.ParentBookingId = parent.Id; _db.Bookings.Add(child); }
                await _db.SaveChangesAsync();

                await _db.Entry(parent).Reference(b => b.Resource).LoadAsync();
                await _db.Entry(parent).Reference(b => b.User).LoadAsync();
                try { await _emailService.SendBookingConfirmationAsync(parent); } catch { }
                TempData["SuccessMessage"] = $"Recurring booking created successfully! {all.Count} bookings were created. Confirmation email sent.";
                return RedirectToPage("/Bookings/Index");
            }

            // Single booking save
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            await _db.Entry(booking).Reference(b => b.Resource).LoadAsync();
            await _db.Entry(booking).Reference(b => b.User).LoadAsync();
            try { await _emailService.SendBookingConfirmationAsync(booking); } catch { }
            TempData["SuccessMessage"] = "Booking created successfully! Confirmation email sent.";
            return RedirectToPage("/Bookings/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while creating the booking: " + ex.Message);
            return Page();
        }
    }
}
