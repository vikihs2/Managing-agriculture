using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Threading.Tasks;
using System.Linq;

namespace ManagingAgriculture.Controllers
{
    /// <summary>
    /// Fields management for Boss, Admin, IT Support, and regular Users.
    /// Employees and Managers can VIEW fields but not manage them (they need them for plant tracking).
    /// </summary>
    [Authorize]
    public class FieldsController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public FieldsController(ManagingAgriculture.Data.ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<List<Field>> GetUserFields(ApplicationUser user)
        {
            if (user.CompanyId != null)
                return await _context.Fields.Where(f => f.CompanyId == user.CompanyId).ToListAsync();
            else
                return await _context.Fields.Where(f => f.OwnerUserId == user.Id).ToListAsync();
        }

        private bool CanManageFields()
        {
            // Employees and Managers cannot manage fields (Boss, Admin, ITSupport, User can)
            return !User.IsInRole("Employee") && !User.IsInRole("Manager");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Fields";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var fields = await GetUserFields(user);
            ViewBag.CanManage = CanManageFields();
            return View(fields);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!CanManageFields()) return Forbid();
            ViewData["Title"] = "Add Field";
            return View(new Field());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Field model)
        {
            if (!CanManageFields()) return Forbid();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Add Field";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var field = new Field
            {
                Name = model.Name,
                SizeInDecars = model.SizeInDecars,
                City = model.City,
                SoilType = model.SoilType,
                SunlightExposure = model.SunlightExposure,
                AverageTemperatureCelsius = model.AverageTemperatureCelsius,
                IsOccupied = false,
                CompanyId = user.CompanyId,
                OwnerUserId = user.CompanyId == null ? user.Id : null,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _context.Fields.Add(field);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Field created successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanManageFields()) return Forbid();
            ViewData["Title"] = "Edit Field";

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null) return NotFound();

            // Ownership check
            if (user.CompanyId != null && field.CompanyId != user.CompanyId) return Forbid();
            if (user.CompanyId == null && field.OwnerUserId != user.Id) return Forbid();

            return View(field);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Field model)
        {
            if (!CanManageFields()) return Forbid();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Edit Field";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null) return NotFound();

            if (user.CompanyId != null && field.CompanyId != user.CompanyId) return Forbid();
            if (user.CompanyId == null && field.OwnerUserId != user.Id) return Forbid();

            field.Name = model.Name;
            field.SizeInDecars = model.SizeInDecars;
            field.City = model.City;
            field.SoilType = model.SoilType;
            field.SunlightExposure = model.SunlightExposure;
            field.AverageTemperatureCelsius = model.AverageTemperatureCelsius;
            field.UpdatedDate = DateTime.UtcNow;

            _context.Fields.Update(field);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Field updated!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanManageFields()) return Forbid();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null) return NotFound();

            if (user.CompanyId != null && field.CompanyId != user.CompanyId) return Forbid();
            if (user.CompanyId == null && field.OwnerUserId != user.Id) return Forbid();

            if (field.IsOccupied)
            {
                TempData["Error"] = "Cannot delete an occupied field. Harvest the current plant first.";
                return RedirectToAction(nameof(Index));
            }

            _context.Fields.Remove(field);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Field deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
