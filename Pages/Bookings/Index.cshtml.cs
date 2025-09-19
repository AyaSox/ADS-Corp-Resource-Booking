using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using System.Linq;

namespace ResourceBooking.Pages.Bookings;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Data for the page
    public List<Booking> Items { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public string? CurrentUserId { get; set; }

    // Filters
    [BindProperty(SupportsGet = true)]
    public int? ResourceId { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Paging
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 15;

    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUserId = _userManager.GetUserId(User);

        // Resources for filter dropdown
        Resources = await _db.Resources.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

        var q = _db.Bookings
            .AsNoTracking()
            .Include(b => b.Resource)
            .Include(b => b.User)
            .Where(b => !b.Cancelled)
            .AsQueryable();

        if (ResourceId.HasValue)
            q = q.Where(b => b.ResourceId == ResourceId.Value);

        if (Date.HasValue)
            q = q.Where(b => b.StartTime.Date == Date.Value.Date);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            q = q.Where(b => b.Resource.Name.Contains(Search) ||
                             b.Purpose.Contains(Search) ||
                             b.User.FirstName.Contains(Search) ||
                             b.User.LastName.Contains(Search));
        }

        TotalCount = await q.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
        if (CurrentPage < 1) CurrentPage = 1;
        if (TotalPages == 0) TotalPages = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        Items = await q
            .OrderBy(b => b.StartTime)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return Page();
    }
}
