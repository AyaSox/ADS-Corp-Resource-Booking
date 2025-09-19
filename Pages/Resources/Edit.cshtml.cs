using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Pages.Resources;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) { _db = db; }

    [BindProperty]
    public Resource Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var res = await _db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (res == null) return NotFound();
        Input = res;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var existing = await _db.Resources.FirstOrDefaultAsync(r => r.Id == Input.Id);
        if (existing == null) return NotFound();
        existing.Name = Input.Name;
        existing.Description = Input.Description;
        existing.Location = Input.Location;
        existing.Capacity = Input.Capacity;
        existing.IsAvailable = Input.IsAvailable;
        existing.UnavailabilityReason = Input.UnavailabilityReason;
        existing.UnavailableUntil = Input.UnavailableUntil;
        existing.UnavailabilityType = Input.UnavailabilityType;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Resource updated.";
        return RedirectToPage("/Resources/Index");
    }
}
