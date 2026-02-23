using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ManagingAgriculture.Models.ApplicationUser> _userManager;

        public DashboardController(ManagingAgriculture.Data.ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ManagingAgriculture.Models.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Dashboard";

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            IQueryable<ManagingAgriculture.Models.Plant> plantsQuery = _context.Plants.Include(p => p.Field);
            IQueryable<ManagingAgriculture.Models.Resource> resourcesQuery = _context.Resources;
            IQueryable<ManagingAgriculture.Models.Machinery> machineryQuery = _context.Machinery;
            IQueryable<ManagingAgriculture.Models.HarvestRecord> harvestQuery = _context.HarvestRecords;

            if (user.CompanyId != null)
            {
                plantsQuery = plantsQuery.Where(p => p.CompanyId == user.CompanyId);
                resourcesQuery = resourcesQuery.Where(r => r.CompanyId == user.CompanyId);
                machineryQuery = machineryQuery.Where(m => m.CompanyId == user.CompanyId);
                harvestQuery = harvestQuery.Where(h => h.CompanyId == user.CompanyId);
            }
            else
            {
                plantsQuery = plantsQuery.Where(p => p.OwnerUserId == user.Id);
                resourcesQuery = resourcesQuery.Where(r => r.OwnerUserId == user.Id);
                machineryQuery = machineryQuery.Where(m => m.OwnerUserId == user.Id);
                harvestQuery = harvestQuery.Where(h => h.OwnerUserId == user.Id);
            }

            var viewModel = new ManagingAgriculture.ViewModels.DashboardViewModel
            {
                ActivePlantsCount = await plantsQuery.CountAsync(p => p.Status != "Harvested"),
                ResourcesCount = await resourcesQuery.CountAsync(),
                MachineryCount = await machineryQuery.CountAsync(),
                LowStockCount = await resourcesQuery.CountAsync(r => r.Quantity <= r.LowStockThreshold),
                ActiveCrops = await plantsQuery
                    .Where(p => p.Status != "Harvested")
                    .OrderByDescending(p => p.CreatedDate)
                    .Take(5)
                    .ToListAsync(),
                RecentHarvests = await harvestQuery
                    .OrderByDescending(h => h.HarvestedDate)
                    .Take(20)
                    .ToListAsync()
            };

            // Pass Company Name if exists
            if (user.CompanyId != null)
            {
                var company = await _context.Companies.FindAsync(user.CompanyId);
                ViewBag.CompanyName = company?.Name;
                ViewBag.CompanyLogo = company?.LogoPath;
            }

            // Check for Pending Invitations for this user's email
            var pendingInvites = await _context.CompanyInvitations
                .Include(i => i.Company)
                .Where(i => i.Email == user.Email && !i.IsUsed)
                .ToListAsync();
            
            ViewBag.PendingInvites = pendingInvites;

            // Count unread messages for notification badge
            var unreadMessages = await _context.ContactForms
                .CountAsync(m => m.Email == user.Email && !m.IsReplied);
            ViewBag.UnreadMessageCount = unreadMessages;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvite(int inviteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var invite = await _context.CompanyInvitations.FindAsync(inviteId);
            if (invite != null && invite.Email == user.Email && !invite.IsUsed)
            {
                user.CompanyId = invite.CompanyId;
                if (invite.Salary > 0) user.Salary = invite.Salary;
                if (invite.LeaveDays > 0) user.LeaveDaysTotal = invite.LeaveDays;
                await _userManager.UpdateAsync(user);
                
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, invite.Role);

                invite.IsUsed = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineInvite(int inviteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var invite = await _context.CompanyInvitations.FindAsync(inviteId);
            if (invite != null && invite.Email == user.Email && !invite.IsUsed)
            {
                _context.CompanyInvitations.Remove(invite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
