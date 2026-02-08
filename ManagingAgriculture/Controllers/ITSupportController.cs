using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ManagingAgriculture.Controllers
{
    [Authorize(Roles = "ITSupport")]
    public class ITSupportController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        public ITSupportController(ManagingAgriculture.Data.ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
             var messages = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_context.ContactForms.OrderByDescending(m => m.CreatedDate));
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> RunDiagnostics()
        {
            try
            {
                // Get system diagnostics
                var diagnostics = new Dictionary<string, object>
                {
                    { "Timestamp", DateTime.UtcNow },
                    { "Database Status", "Connected ✅" },
                    { "Total Users", _context.Users.Count() },
                    { "Total Companies", _context.Companies.Count() },
                    { "Active Plants", _context.Plants.Count(p => p.Status != "Harvested") },
                    { "Total Resources", _context.Resources.Count() },
                    { "Total Machinery", _context.Machinery.Count() },
                    { "Pending Support Tickets", _context.ContactForms.Count(c => !c.IsReplied) },
                    { "Resolved Support Tickets", _context.ContactForms.Count(c => c.IsReplied) },
                    { "Memory (MB)", GC.GetTotalMemory(false) / 1024 / 1024 },
                    { "System Status", "Healthy ✅" }
                };

                return Json(new { success = true, diagnostics = diagnostics });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message, status = "Error ❌" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyToMessage(int id, string replyContent)
        {
            var msg = await _context.ContactForms.FindAsync(id);
            if (msg != null)
            {
                msg.ReplyMessage = replyContent;
                msg.IsReplied = true;
                msg.RepliedDate = System.DateTime.UtcNow;
                msg.RepliedBy = "IT Support"; 
                
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
