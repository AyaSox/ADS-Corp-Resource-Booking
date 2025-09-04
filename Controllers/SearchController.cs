using Microsoft.AspNetCore.Mvc;
using ResourceBooking.Data;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Models;

namespace ResourceBooking.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ApplicationDbContext db, ILogger<SearchController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return View(new SearchResultsViewModel { Query = string.Empty });
            }

            try
            {
                var query = q.Trim();

                // Search resources (case-insensitive)
                var resources = await _db.Resources
                    .Where(r => (r.Name != null && EF.Functions.Like(r.Name, "%" + query + "%")) ||
                                (r.Description != null && EF.Functions.Like(r.Description, "%" + query + "%")) ||
                                (r.Location != null && EF.Functions.Like(r.Location, "%" + query + "%")))
                    .OrderBy(r => r.Name)
                    .Take(10)
                    .ToListAsync();

                // Search bookings
                var bookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Include(b => b.User)
                    .Where(b => !b.Cancelled &&
                               ((b.Purpose != null && EF.Functions.Like(b.Purpose, "%" + query + "%")) ||
                                (b.Resource.Name != null && EF.Functions.Like(b.Resource.Name, "%" + query + "%")) ||
                                (b.User.FirstName != null && EF.Functions.Like(b.User.FirstName, "%" + query + "%")) ||
                                (b.User.LastName != null && EF.Functions.Like(b.User.LastName, "%" + query + "%"))))
                    .OrderByDescending(b => b.StartTime)
                    .Take(15)
                    .ToListAsync();

                // Search users
                var users = await _db.Users
                    .Where(u => (u.FirstName != null && EF.Functions.Like(u.FirstName, "%" + query + "%")) ||
                                (u.LastName != null && EF.Functions.Like(u.LastName, "%" + query + "%")) ||
                                (u.Email != null && EF.Functions.Like(u.Email, "%" + query + "%")))
                    .OrderBy(u => u.FirstName)
                    .Take(8)
                    .ToListAsync();

                var model = new SearchResultsViewModel
                {
                    Query = q,
                    Resources = resources,
                    Bookings = bookings,
                    Users = users
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search for query: {Query}", q);
                return View(new SearchResultsViewModel { Query = q });
            }
        }

        [HttpGet]
        public async Task<IActionResult> QuickSearch(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(new { resources = new List<object>(), bookings = new List<object>() });
            }

            try
            {
                var query = q.Trim();

                var resources = await _db.Resources
                    .Where(r => (r.Name != null && EF.Functions.Like(r.Name, "%" + query + "%")) ||
                                (r.Location != null && EF.Functions.Like(r.Location, "%" + query + "%")))
                    .OrderBy(r => r.Name)
                    .Take(5)
                    .Select(r => new { r.Id, r.Name, r.Location, r.IsAvailable })
                    .ToListAsync();

                var bookings = await _db.Bookings
                    .Include(b => b.Resource)
                    .Where(b => !b.Cancelled && (b.Purpose != null && EF.Functions.Like(b.Purpose, "%" + query + "%")))
                    .OrderByDescending(b => b.StartTime)
                    .Take(5)
                    .Select(b => new { b.Id, b.Purpose, ResourceName = b.Resource.Name, b.StartTime })
                    .ToListAsync();

                return Json(new { resources, bookings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick search: {Query}", q);
                return Json(new { resources = new List<object>(), bookings = new List<object>() });
            }
        }
    }

    public class SearchResultsViewModel
    {
        public string Query { get; set; } = string.Empty;
        public List<Resource> Resources { get; set; } = new();
        public List<Booking> Bookings { get; set; } = new();
        public List<ApplicationUser> Users { get; set; } = new();

        public int TotalResults => (Resources?.Count ?? 0) + (Bookings?.Count ?? 0) + (Users?.Count ?? 0);
    }
}