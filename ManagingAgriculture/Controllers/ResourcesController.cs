using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ManagingAgriculture.Models;
using ManagingAgriculture.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Identity; // Added for UserManager

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class ResourcesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager; // Added for UserManager

        public ResourcesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) // Modified constructor
        {
            _context = context;
            _userManager = userManager; // Initialized UserManager
        }

        /// <summary>
        /// Display all resources, optionally filtered by category
        /// </summary>
        public async Task<IActionResult> Index(string? category = null)
        {
            ViewData["Title"] = "Resource Management";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            var query = _context.Resources.AsQueryable();

            // If user belongs to a company, show only company resources
            if (user.CompanyId != null)
            {
                 query = query.Where(r => r.CompanyId == user.CompanyId);
            }
            else
            {
                // If freelancer, show only their own resources
                 query = query.Where(r => r.OwnerUserId == user.Id);
            }
            
            // Filter by category if provided
            if (!string.IsNullOrEmpty(category) && category != "all")
            {
                query = query.Where(r => r.Category == category);
            }

            var resources = await query.ToListAsync();
            
            // Get all categories for the filter dropdown
            ViewBag.Categories = await _context.Resources.Select(r => r.Category).Distinct().ToListAsync();
            ViewBag.SelectedCategory = category ?? "all";
            
            return View(resources);
        }

        [HttpGet]
        public IActionResult Add()
        {
            // Only Boss and Manager can add resources
            if (User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot add resources.";
                return RedirectToAction("Index");
            }
            ViewData["Title"] = "Add Resource";
            return View(new Resource());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Resource model)
        {
            // Only Boss and Manager can add resources
            if (User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot add resources.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                model.OwnerUserId = user.Id;
                model.CompanyId = user.CompanyId; // Null if freelancer
            }

            model.CreatedDate = DateTime.Now;
            model.UpdatedDate = DateTime.Now;
            
            _context.Resources.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Only Boss and Manager can edit resources
            if (User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot edit resources.";
                return RedirectToAction("Index");
            }
            var item = await _context.Resources.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Resource model)
        {
            // Only Boss and Manager can edit
            if (User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot edit resources.";
                return RedirectToAction("Index");
            }

            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid) return View(model);

            try
            {
                var existing = await _context.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
                if (existing == null) return NotFound();

                model.CreatedDate = existing.CreatedDate;
                model.UpdatedDate = DateTime.Now;
                model.OwnerUserId = existing.OwnerUserId;
                model.CompanyId = existing.CompanyId;
                
                // Detach existing to avoid tracking conflict
                _context.Entry(existing).State = EntityState.Detached;

                _context.Update(model);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ResourceExists(id)) return NotFound();
                else throw;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Only Boss can delete resources
            if (!User.IsInRole("Boss"))
            {
                TempData["Error"] = "Only the Boss can delete resources.";
                return RedirectToAction("Index");
            }

            var item = await _context.Resources.FindAsync(id);
            if (item != null)
            {
                _context.Resources.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Resource deleted successfully.";
            }
            return RedirectToAction("Index");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustQuantity(int id, int delta)
        {
            var item = await _context.Resources.FindAsync(id);
            if (item != null)
            {
                item.Quantity = Math.Max(0, item.Quantity + delta);
                item.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// AJAX endpoint to adjust resource quantity and return updated value
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AdjustQuantityAjax([FromBody] AdjustRequest req)
        {
            var item = await _context.Resources.FindAsync(req.Id);
            if (item == null)
                return Json(new { success = false });

            item.Quantity = Math.Max(0, item.Quantity + req.Delta);
            item.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, quantity = item.Quantity });
        }

        private bool ResourceExists(int id)
        {
            return _context.Resources.Any(e => e.Id == id);
        }

        public class AdjustRequest
        {
            public int Id { get; set; }
            public int Delta { get; set; }
        }
    }
}
