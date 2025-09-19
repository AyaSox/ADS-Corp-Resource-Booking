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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailService emailService, INotificationService notificationService)
    {
        _db = db; _userManager = userManager; _emailService = emailService; _notificationService = notificationService;
    }

    public List<Resource> Resources { get; set; } = new();

    [BindProperty]
    public EditBookingViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
        var booking = await _db.Bookings.Include(b => b.User).FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound();
        var currentUserId = _userManager.GetUserId(User);
        if (booking.UserId != currentUserId)
        {
            TempData["ErrorMessage"] = "You can only edit your own bookings.";
            return RedirectToPage("/Bookings/Index");
        }
        Input = EditBookingViewModel.FromBooking(booking);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Resources = await _db.Resources.OrderBy(r => r.Name).ToListAsync();
        if (!ModelState.IsValid) return Page();

        var currentUserId = _userManager.GetUserId(User);
        if (Input.UserId != currentUserId)
        {
            TempData["ErrorMessage"] = "You can only edit your own bookings.";
            return RedirectToPage("/Bookings/Index");
        }

        // Proposed UTC range
        var startUtc = TimeZoneHelper.ConvertToUtc(Input.StartTime);
        var endUtc = TimeZoneHelper.ConvertToUtc(Input.EndTime);
        var windowStart = startUtc.Date.AddDays(-1);
        var windowEnd = endUtc.Date.AddDays(1);
        var candidates = await _db.Bookings.Where(b => b.ResourceId == Input.ResourceId && !b.Cancelled && b.Id != Input.Id && b.EndTime > windowStart && b.StartTime < windowEnd).ToListAsync();
        if (candidates.Any(b => startUtc < b.EndTime && b.StartTime < endUtc))
        {
            ModelState.AddModelError("", "This resource is already booked during the requested time.");
            return Page();
        }

        try
        {
            var existing = await _db.Bookings.Include(b => b.Resource).Include(b => b.User).FirstOrDefaultAsync(b => b.Id == Input.Id);
            if (existing == null) return NotFound();
            var wasCancelled = existing.Cancelled;
            Input.UpdateBooking(existing);
            await _db.SaveChangesAsync();

            if (!wasCancelled && existing.Cancelled)
            {
                try { await _emailService.SendBookingCancellationAsync(existing); } catch { }
            }
            TempData["SuccessMessage"] = existing.Cancelled ? "Booking cancelled successfully! Cancellation email sent." : "Booking updated successfully!";
            try { await _notificationService.CreateSystemNotificationAsync(existing.UserId, existing.Cancelled ? "Booking cancelled" : "Booking updated", $"Your booking for {existing.Resource.Name} has been " + (existing.Cancelled ? "cancelled" : "updated") + ".", existing.Id); } catch { }
            return RedirectToPage("/Bookings/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while updating the booking: " + ex.Message);
            return Page();
        }
    }
}
