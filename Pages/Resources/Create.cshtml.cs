using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Pages.Resources;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) { _db = db; }

    [BindProperty]
    public Resource Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        _db.Resources.Add(Input);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Resource created.";
        return RedirectToPage("/Resources/Index");
    }
}
