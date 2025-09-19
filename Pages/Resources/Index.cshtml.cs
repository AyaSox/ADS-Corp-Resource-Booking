using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Pages.Resources;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) { _db = db; }

    public List<Resource> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _db.Resources.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }
}
