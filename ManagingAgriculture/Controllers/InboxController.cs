using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class InboxController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public InboxController(ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            bool isAdminOrIT = User.IsInRole("SystemAdmin") || User.IsInRole("ITSupport");

            if (isAdminOrIT)
            {
                // Admin/IT see ALL contact form messages (with reply functionality)
                var allMessages = await _context.ContactForms
                    .OrderByDescending(m => m.CreatedDate)
                    .ToListAsync();

                return View("AdminInbox", allMessages);
            }
            else
            {
                // Regular users see only their own messages (matched by email)
                var messages = await _context.ContactForms
                    .Where(m => m.Email == user.Email)
                    .OrderByDescending(m => m.CreatedDate)
                    .ToListAsync();

                // Mark replied messages as read → clears notification badge
                foreach (var msg in messages.Where(m => m.IsReplied && !m.IsReadByUser))
                    msg.IsReadByUser = true;

                if (messages.Any(m => m.IsReplied))
                    await _context.SaveChangesAsync();

                return View(messages);
            }
        }

        /// <summary>Admin/IT reply to a message directly from inbox.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SystemAdmin,ITSupport")]
        public async Task<IActionResult> Reply(int id, string replyContent)
        {
            var msg = await _context.ContactForms.FindAsync(id);
            if (msg != null)
            {
                msg.ReplyMessage = replyContent;
                msg.IsReplied = true;
                msg.RepliedDate = System.DateTime.UtcNow;
                msg.IsReadByUser = false; // Unread again for the sender

                var user = await _userManager.GetUserAsync(User);
                msg.RepliedBy = User.IsInRole("SystemAdmin") ? "Admin" : "IT Support";

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
