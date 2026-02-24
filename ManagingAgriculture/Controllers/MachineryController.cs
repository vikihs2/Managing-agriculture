using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ManagingAgriculture.Models;
using ManagingAgriculture.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace ManagingAgriculture.Controllers
{
    /// <summary>
    /// MachineryController manages farm equipment inventory and maintenance tracking.
    /// Handles viewing, adding, editing, and deleting machinery records with maintenance history.
    /// </summary>
    [Authorize]
    public class MachineryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public MachineryController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Displays list of all machinery in the farm inventory.
        /// Returns machinery sorted by status (Excellent first).
        /// </summary>
        /// <returns>View with IEnumerable of Machinery</returns>
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Machinery";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            List<Machinery> machineryList;
            if (user.CompanyId != null)
            {
                machineryList = await _context.Machinery
                    .Where(m => m.CompanyId == user.CompanyId || (m.CompanyId == null && m.OwnerUserId == user.Id))
                    .ToListAsync();
            }
            else
            {
                machineryList = await _context.Machinery.Where(m => m.OwnerUserId == user.Id).ToListAsync();
            }

            return View(machineryList);
        }

        [HttpGet]
        public async Task<IActionResult> DetailsApi(int id)
        {
            var mach = await _context.Machinery.FindAsync(id);
            if (mach == null) return NotFound();
            return Json(new { id = mach.Id, name = mach.Name, type = mach.Type, status = mach.Status });
        }

        /// <summary>
        /// Displays form to add new machinery.
        /// HTTP GET method for displaying empty form.
        /// </summary>
        /// <returns>View with empty Machinery model</returns>
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Company users: only Boss can add machinery
            if (user.CompanyId != null && !User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can add machinery.";
                return RedirectToAction(nameof(Index));
            }
            return View(new Machinery());
        }

        /// <summary>
        /// Processes form submission to create new machinery record.
        /// HTTP POST method that validates input and adds machinery to collection.
        /// </summary>
        /// <param name="machinery">Machinery object populated from form submission</param>
        /// <returns>Redirects to Index on success, returns view with model on validation failure</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Machinery machinery)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Company users: only Boss can add machinery
            if (user.CompanyId != null && !User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can add machinery.";
                return RedirectToAction(nameof(Index));
            }

            // Validate model state
            if (!ModelState.IsValid)
            {
                return View(machinery);
            }

            // Additional validation: Engine hours must not be negative
            if (machinery.EngineHours.HasValue && machinery.EngineHours.Value < 0)
            {
                ModelState.AddModelError(nameof(machinery.EngineHours), "Engine hours cannot be negative");
                return View(machinery);
            }

            // user already fetched above — set ownership
            machinery.OwnerUserId = user.Id;
            machinery.CompanyId = user.CompanyId;

            // Set timestamps for audit trail
            machinery.CreatedDate = DateTime.Now;
            machinery.UpdatedDate = DateTime.Now;
            
            // Add to database
            _context.Machinery.Add(machinery);
            await _context.SaveChangesAsync();

            // Redirect to machinery list view
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays machinery details and edit form.
        /// HTTP GET method for displaying machinery information by ID.
        /// </summary>
        /// <param name="id">ID of machinery to display</param>
        /// <returns>View with Machinery model if found, or NotFound if machinery doesn't exist</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Company users: only Boss can edit
            if (user.CompanyId != null && !User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can edit machinery.";
                return RedirectToAction(nameof(Index));
            }
            // Find machinery by ID
            var machinery = await _context.Machinery.FindAsync(id);
            if (machinery == null) return NotFound();

            // Non-company users can only edit their own machinery
            if (user.CompanyId == null && machinery.OwnerUserId != user.Id)
                return Forbid();

            return View(machinery);
        }

        /// <summary>
        /// Processes form submission to update existing machinery record.
        /// HTTP POST method that validates input and updates machinery in collection.
        /// </summary>
        /// <param name="id">ID of machinery to update</param>
        /// <param name="machinery">Updated Machinery object from form submission</param>
        /// <returns>Redirects to Index on success, returns view with model on validation failure</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Machinery machinery)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Company users: only Boss can edit machinery
            if (user.CompanyId != null && !User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can edit machinery.";
                return RedirectToAction(nameof(Index));
            }

            if (id != machinery.Id)
            {
                return NotFound();
            }

            // Validate model state
            if (!ModelState.IsValid)
            {
                return View(machinery);
            }

            // Additional validation: Engine hours must not be negative
            if (machinery.EngineHours.HasValue && machinery.EngineHours.Value < 0)
            {
                ModelState.AddModelError(nameof(machinery.EngineHours), "Engine hours cannot be negative");
                return View(machinery);
            }

            try
            {
                var existingMachinery = await _context.Machinery.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                if (existingMachinery == null)
                {
                    return NotFound();
                }

                // Preserve creation date and ownership
                machinery.CreatedDate = existingMachinery.CreatedDate;
                machinery.UpdatedDate = DateTime.Now;
                machinery.OwnerUserId = existingMachinery.OwnerUserId;
                machinery.CompanyId = existingMachinery.CompanyId;

                // Detach existing to avoid tracking conflict
                _context.Entry(existingMachinery).State = EntityState.Detached;

                _context.Update(machinery);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MachineryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Redirect to machinery list view
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Deletes machinery record from inventory.
        /// Removes the machinery with specified ID from collection.
        /// </summary>
        /// <param name="id">ID of machinery to delete</param>
        /// <returns>Redirects to Index after deletion</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Company users: only Boss can delete machinery
            if (user.CompanyId != null && !User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can delete machinery.";
                return RedirectToAction(nameof(Index));
            }

            // Find and remove machinery from database
            var machinery = await _context.Machinery.FindAsync(id);

            if (machinery != null)
            {
                // Non-company users can only delete their own machinery
                if (user.CompanyId == null && machinery.OwnerUserId != user.Id)
                    return Forbid();

                // Prevent delete if it's currently on sale
                var isListed = await _context.MarketplaceListings
                    .AnyAsync(ml => ml.MachineryId == machinery.Id && ml.ListingStatus == "Active");

                if (isListed)
                {
                    TempData["Error"] = "Cannot delete machinery that is currently listed for sale in the marketplace. Please cancel the listing first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Machinery.Remove(machinery);
                await _context.SaveChangesAsync();
            }

            // Redirect to machinery list view
            return RedirectToAction(nameof(Index));
        }

        private bool MachineryExists(int id)
        {
            return _context.Machinery.Any(e => e.Id == id);
        }
    }
}
