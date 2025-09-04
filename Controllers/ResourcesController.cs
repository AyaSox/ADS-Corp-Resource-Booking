using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResourceBooking.Data;
using ResourceBooking.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ResourceBooking.Services;

namespace ResourceBooking.Controllers
{
    public class ResourcesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ResourcesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;
        
        public ResourcesController(ApplicationDbContext db, ILogger<ResourcesController> logger, UserManager<ApplicationUser> userManager, INotificationService notificationService) 
        { 
            _db = db; 
            _logger = logger;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: Resources
        public async Task<IActionResult> Index(string search)
        {
            var query = _db.Resources.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Name.Contains(search) || r.Location.Contains(search));
            }
            var list = await query.OrderBy(r => r.Name).ToListAsync();
            ViewBag.CurrentSearch = search;
            return View(list);
        }

        // GET: Resources/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var resource = await _db.Resources
                .Include(r => r.Bookings.Where(b => !b.Cancelled && b.EndTime >= System.DateTime.UtcNow).OrderBy(b => b.StartTime))
                .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resource == null) return NotFound();
            return View(resource);
        }

        // GET: Resources/Create
        public IActionResult Create() => View();

        // POST: Resources/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Location,Capacity,IsAvailable,UnavailabilityReason,UnavailableUntil,UnavailabilityType")] Resource resource)
        {
            _logger.LogInformation("Creating resource - Name: {Name}, Location: {Location}, Capacity: {Capacity}, IsAvailable: {IsAvailable}", 
                resource.Name, resource.Location, resource.Capacity, resource.IsAvailable);

            ModelState.Remove("Bookings");

            if (resource.IsAvailable)
            {
                resource.UnavailabilityReason = null;
                resource.UnavailableUntil = null;
                resource.UnavailabilityType = null;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Add(resource);
                    await _db.SaveChangesAsync();
                    
                    _logger.LogInformation("Resource created successfully with ID: {ResourceId}", resource.Id);
                    TempData["SuccessMessage"] = "Resource created successfully!";

                    // In-app notification for the actor
                    var userId = _userManager.GetUserId(User);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        try { await _notificationService.CreateSystemNotificationAsync(userId, "Resource created", $"Resource '{resource.Name}' was created."); } catch { }
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating resource: {Message}", ex.Message);
                    ModelState.AddModelError("", "An error occurred while creating the resource: " + ex.Message);
                }
            }
            else
            {
                foreach (var error in ModelState)
                {
                    _logger.LogWarning("Model validation error - Key: {Key}, Errors: {Errors}", 
                        error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                }
            }

            return View(resource);
        }

        // GET: Resources/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var resource = await _db.Resources.FindAsync(id);
            if (resource == null) return NotFound();
            return View(resource);
        }

        // POST: Resources/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Location,Capacity,IsAvailable,UnavailabilityReason,UnavailableUntil,UnavailabilityType")] Resource resource)
        {
            if (id != resource.Id) return NotFound();
            
            _logger.LogInformation("Editing resource ID: {ResourceId} - Name: {Name}, Location: {Location}, Capacity: {Capacity}, IsAvailable: {IsAvailable}", 
                resource.Id, resource.Name, resource.Location, resource.Capacity, resource.IsAvailable);

            ModelState.Remove("Bookings");

            if (resource.IsAvailable)
            {
                resource.UnavailabilityReason = null;
                resource.UnavailableUntil = null;
                resource.UnavailabilityType = null;
                _logger.LogInformation("Resource marked as available - clearing unavailability details");
            }
            else
            {
                _logger.LogInformation("Resource marked as unavailable - Type: {Type}, Reason: {Reason}", 
                    resource.UnavailabilityType, resource.UnavailabilityReason);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Update(resource);
                    await _db.SaveChangesAsync();
                    
                    _logger.LogInformation("Resource updated successfully");
                    TempData["SuccessMessage"] = "Resource updated successfully!";

                    var userId = _userManager.GetUserId(User);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        try { await _notificationService.CreateSystemNotificationAsync(userId, "Resource updated", $"Resource '{resource.Name}' was updated."); } catch { }
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!_db.Resources.Any(e => e.Id == resource.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError(ex, "Concurrency error updating resource: {Message}", ex.Message);
                        ModelState.AddModelError("", "The resource was modified by another user. Please reload and try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating resource: {Message}", ex.Message);
                    ModelState.AddModelError("", "An error occurred while updating the resource: " + ex.Message);
                }
            }
            else
            {
                foreach (var error in ModelState)
                {
                    _logger.LogWarning("Model validation error - Key: {Key}, Errors: {Errors}", 
                        error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                }
            }

            return View(resource);
        }

        // GET: Resources/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var resource = await _db.Resources.FirstOrDefaultAsync(m => m.Id == id);
            if (resource == null) return NotFound();
            return View(resource);
        }

        // POST: Resources/Delete/5 (Actually marks as unavailable)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string deactivationReason, string? additionalNotes)
        {
            _logger.LogInformation("Attempting to deactivate resource {ResourceId} with reason: {Reason}", id, deactivationReason);
            
            try
            {
                var resource = await _db.Resources
                    .FirstOrDefaultAsync(r => r.Id == id);
                    
                if (resource == null)
                {
                    _logger.LogWarning("Resource with ID {ResourceId} not found", id);
                    TempData["ErrorMessage"] = "Resource not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate deactivation reason
                if (!Enum.TryParse<UnavailabilityType>(deactivationReason, out var reasonEnum) ||
                    (reasonEnum != UnavailabilityType.DoesNotExist && 
                     reasonEnum != UnavailabilityType.AddedMistakenly && 
                     reasonEnum != UnavailabilityType.Retired))
                {
                    _logger.LogWarning("Invalid deactivation reason: {Reason}", deactivationReason);
                    TempData["ErrorMessage"] = "Invalid deactivation reason provided.";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                _logger.LogInformation("Deactivating resource '{ResourceName}' (ID: {ResourceId}) with reason: {Reason}", 
                    resource.Name, id, reasonEnum);

                // Mark as permanently unavailable
                resource.IsAvailable = false;
                resource.UnavailabilityType = reasonEnum;
                resource.UnavailableUntil = null; // Permanent deactivation - no end date
                
                // Build deactivation reason text
                var reasonText = reasonEnum switch
                {
                    UnavailabilityType.DoesNotExist => "Resource does not exist",
                    UnavailabilityType.AddedMistakenly => "Resource was added mistakenly",
                    UnavailabilityType.Retired => "Resource has been retired from service",
                    _ => "Resource has been deactivated"
                };

                if (!string.IsNullOrWhiteSpace(additionalNotes))
                {
                    reasonText += $" - {additionalNotes.Trim()}";
                }

                resource.UnavailabilityReason = reasonText;

                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Resource deactivated successfully: {ResourceName} - {Reason}", resource.Name, reasonText);
                TempData["SuccessMessage"] = $"Resource '{resource.Name}' has been permanently deactivated: {reasonEnum.ToString().Replace("DoesNotExist", "Does Not Exist").Replace("AddedMistakenly", "Added Mistakenly")}.";

                var userId = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(userId))
                {
                    try 
                    { 
                        await _notificationService.CreateSystemNotificationAsync(userId, "Resource deactivated", 
                            $"Resource '{resource.Name}' has been permanently deactivated: {reasonEnum.ToString().Replace("DoesNotExist", "Does Not Exist").Replace("AddedMistakenly", "Added Mistakenly")}."); 
                    } 
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating resource {ResourceId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = $"Unable to deactivate resource. Error: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}