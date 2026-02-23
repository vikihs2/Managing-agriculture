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

        public InboxController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var messages = await _context.ContactForms
                .Where(m => m.Email == user.Email)
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();

            // Mark all replied messages as read (clears the notification badge)
            foreach (var msg in messages.Where(m => m.IsReplied && !m.IsReadByUser))
            {
                msg.IsReadByUser = true;
            }
            if (messages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return View(messages);
        }
    }
}
