using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ResourceBooking.Models;
using ResourceBooking.Services;

namespace ResourceBooking.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Notifications
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            
            return View(notifications);
        }

        // API: Get unread count for navbar badge
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);
            var count = await _notificationService.GetUnreadCountAsync(userId);
            
            return Json(new { count });
        }

        // API: Get recent notifications for dropdown
        [HttpGet]
        public async Task<IActionResult> GetRecent(int take = 5)
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, take);
            
            var result = notifications.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                type = n.Type.ToString(),
                isRead = n.IsRead,
                createdAt = n.RelativeTime,
                icon = n.TypeIcon,
                color = n.TypeColor,
                bookingId = n.BookingId
            });
            
            return Json(result);
        }

        // POST: Mark as read
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            await _notificationService.MarkAsReadAsync(id, userId);
            
            return Json(new { success = true });
        }

        // POST: Mark all as read
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            await _notificationService.MarkAllAsReadAsync(userId);
            
            TempData["SuccessMessage"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Delete notification
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            await _notificationService.DeleteNotificationAsync(id, userId);
            
            return Json(new { success = true });
        }
    }
}