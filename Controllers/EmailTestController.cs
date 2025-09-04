using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ResourceBooking.Models;
using ResourceBooking.Services;

namespace ResourceBooking.Controllers
{
    [Authorize]
    public class EmailTestController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EmailTestController> _logger;

        public EmailTestController(IEmailService emailService, UserManager<ApplicationUser> userManager, ILogger<EmailTestController> logger)
        {
            _emailService = emailService;
            _userManager = userManager;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendTestEmail(string toEmail, string subject, string body)
        {
            try
            {
                await _emailService.SendTestEmailAsync(toEmail, subject, body);
                TempData["EmailResult"] = $"Test email sent successfully to {toEmail}";
                _logger.LogInformation("Test email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                TempData["EmailResult"] = $"Failed to send email: {ex.Message}";
                _logger.LogError(ex, "Failed to send test email to {Email}", toEmail);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SendWelcomeEmail(string toEmail, string userName)
        {
            try
            {
                // Create a temporary user object for testing
                var testUser = new ApplicationUser
                {
                    Email = toEmail,
                    UserName = toEmail,
                    FirstName = userName.Split(' ').FirstOrDefault() ?? "Test",
                    LastName = userName.Split(' ').LastOrDefault() ?? "User",
                    EmailConfirmed = true
                };

                await _emailService.SendWelcomeEmailAsync(testUser);
                TempData["EmailResult"] = $"Welcome email sent successfully to {toEmail}";
                _logger.LogInformation("Welcome test email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                TempData["EmailResult"] = $"Failed to send welcome email: {ex.Message}";
                _logger.LogError(ex, "Failed to send welcome test email to {Email}", toEmail);
            }

            return RedirectToAction("Index");
        }
    }
}