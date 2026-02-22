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

            if (user.CompanyId != null)
            {
                plantsQuery = plantsQuery.Where(p => p.CompanyId == user.CompanyId || (p.CompanyId == null && p.OwnerUserId == user.Id));
                resourcesQuery = resourcesQuery.Where(r => r.CompanyId == user.CompanyId || (r.CompanyId == null && r.OwnerUserId == user.Id));
                machineryQuery = machineryQuery.Where(m => m.CompanyId == user.CompanyId || (m.CompanyId == null && m.OwnerUserId == user.Id));
            }
            else
            {
                plantsQuery = plantsQuery.Where(p => p.OwnerUserId == user.Id);
                resourcesQuery = resourcesQuery.Where(r => r.OwnerUserId == user.Id);
                machineryQuery = machineryQuery.Where(m => m.OwnerUserId == user.Id);
            }

            var viewModel = new ManagingAgriculture.ViewModels.DashboardViewModel
            {
                ActivePlantsCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(plantsQuery.Where(p => p.Status != "Harvested")),
                ResourcesCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(resourcesQuery),
                MachineryCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(machineryQuery),
                LowStockCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(resourcesQuery.Where(r => r.Quantity <= r.LowStockThreshold)),
                ActiveCrops = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(plantsQuery
                    .Where(p => p.Status != "Harvested")
                    .OrderByDescending(p => p.CreatedDate)
                    .Take(5))
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
                // Join Company
                user.CompanyId = invite.CompanyId;
                await _userManager.UpdateAsync(user);
                
                // Assign new role
                // Remove old roles first? Maybe. Assuming "User" -> "Employee"/"Manager"
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
                // Mark as used (declined) or delete? Let's delete to allow re-invite or mark used.
                // If we mark used, they can't be invited again easily? 
                // Better to just delete the invitation so Boss can re-invite if mistake.
                _context.CompanyInvitations.Remove(invite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
