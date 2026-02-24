using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using ManagingAgriculture.Models;

namespace ManagingAgriculture.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<HomeController> _logger;
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(IConfiguration config, ILogger<HomeController> logger,
            ManagingAgriculture.Data.ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _config = config;
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "AgroCore - Agriculture Management";
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Contact()
        {
            ViewData["Title"] = "Contact - AgroCore";
            ViewBag.SmtpConfigured = !string.IsNullOrWhiteSpace(_config["Smtp:Host"]);
            ViewBag.SmtpHost = _config["Smtp:Host"] ?? string.Empty;

            var form = new ContactForm();

            // Auto-fill user's email
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                form.Email = user.Email ?? "";
                form.FullName = (!string.IsNullOrWhiteSpace(user.FirstName)
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : user.Email) ?? "";
                ViewBag.UserEmail = user.Email;
            }

            return View(form);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactForm form)
        {
            // Force email to logged-in user's email
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                form.Email = user.Email ?? form.Email;
                ModelState.Remove("Email");
            }

            if (!ModelState.IsValid)
                return View(form);

            // Save to Database
            form.CreatedDate = System.DateTime.UtcNow;
            _context.ContactForms.Add(form);
            await _context.SaveChangesAsync();

            var smtpHost = _config["Smtp:Host"];
            if (!string.IsNullOrWhiteSpace(smtpHost))
            {
                try
                {
                    var smtpPort   = int.TryParse(_config["Smtp:Port"],      out var p) ? p : 25;
                    var enableSsl  = bool.TryParse(_config["Smtp:EnableSsl"], out var s) ? s : false;
                    var fromAddress = _config["Smtp:From"] ?? "no-reply@agrocore.local";
                    var toAddress   = _config["Smtp:To"]   ?? "victor.stefanov.highschool@buditel.bg";

                    var msg = new MailMessage(fromAddress, toAddress)
                    {
                        Subject    = $"Contact form: {form.FullName}",
                        Body       = $"Name: {form.FullName}\nEmail: {form.Email}\n\nMessage:\n{form.Message}",
                        IsBodyHtml = false
                    };

                    using var client = new SmtpClient(smtpHost, smtpPort) { EnableSsl = enableSsl };
                    var smtpUser = _config["Smtp:Username"];
                    var smtpPass = _config["Smtp:Password"];
                    if (!string.IsNullOrWhiteSpace(smtpUser))
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    client.Send(msg);
                    TempData["ContactSuccess"] = "Thanks for your message — we've sent it to our support team.";
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Failed to send contact email");
                    TempData["ContactError"] = "There was an error sending your message. We've recorded it and will follow up shortly.";
                }
            }
            else
            {
                _logger.LogInformation("SMTP not configured. Contact from {Email}: {Name}", form.Email, form.FullName);
                TempData["ContactSuccess"] = "Thanks for your message — we will be in touch soon.";
            }

            return RedirectToAction("Contact");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestSmtp()
        {
            var smtpHost = _config["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                TempData["ContactError"] = "SMTP is not configured on this server.";
                return RedirectToAction("Contact");
            }

            try
            {
                var fromAddress = _config["Smtp:From"] ?? "no-reply@agrocore.local";
                var toAddress = _config["Smtp:To"] ?? "victor.stefanov.highschool@buditel.bg";
                var msg = new MailMessage(fromAddress, toAddress)
                {
                    Subject = "AgroCore SMTP test",
                    Body = "This is a test email from AgroCore contact form SMTP check.",
                    IsBodyHtml = false
                };

                var smtpPort = int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;
                var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var s) ? s : false;
                using var client = new SmtpClient(smtpHost, smtpPort) { EnableSsl = enableSsl };
                var user = _config["Smtp:Username"];
                var pass = _config["Smtp:Password"];
                if (!string.IsNullOrWhiteSpace(user)) client.Credentials = new NetworkCredential(user, pass);
                client.Send(msg);
                TempData["ContactSuccess"] = "Test email sent successfully.";
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "SMTP test failed");
                TempData["ContactError"] = "SMTP test failed — check logs and configuration.";
            }

            return RedirectToAction("Contact");
        }
    }
}
