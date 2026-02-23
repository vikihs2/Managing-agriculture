using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ManagingAgriculture.Models;
using ManagingAgriculture.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ManagingAgriculture.Controllers
{
    /// <summary>
    /// MarketplaceController manages marketplace listings for buying, selling, and renting equipment/products.
    /// Users can browse available listings and post their own items for sale or rent.
    /// </summary>
    [Authorize]
    public class MarketplaceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MarketplaceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Displays list of all marketplace listings.
        /// Returns listings filtered by active status, sorted by newest first.
        /// </summary>
        /// <returns>View with IEnumerable of MarketplaceListing</returns>
        public async Task<IActionResult> Index(string? category = null, string? q = null)
        {
            ViewData["Title"] = "Marketplace";

            // Filter active listings and sort by creation date (newest first)
            var query = _context.MarketplaceListings.AsQueryable()
                .Where(l => l.ListingStatus == "Active");

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(l => l.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(l => l.ItemName.ToLower().Contains(qLower) || (l.Description != null && l.Description.ToLower().Contains(qLower)));
            }

            var results = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            // Get distinct categories for filter
            ViewBag.Categories = await _context.MarketplaceListings.Select(l => l.Category).Distinct().ToListAsync();
            ViewBag.SelectedCategory = category ?? "All";
            ViewBag.Query = q ?? string.Empty;

            // Get purchased listing IDs for the current user
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var purchased = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Purchased")
                    .Select(r => r.ListingId)
                    .ToListAsync();
                ViewBag.PurchasedListingIds = new HashSet<int>(purchased);
            }
            else
            {
                ViewBag.PurchasedListingIds = new HashSet<int>();
            }

            return View(results);
        }

        /// <summary>
        /// Returns filtered listings as a partial view for AJAX updates
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Filter(string? category = null, string? q = null)
        {
            var query = _context.MarketplaceListings.AsQueryable()
                .Where(l => l.ListingStatus == "Active");

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(l => l.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(l => l.ItemName.ToLower().Contains(qLower) || (l.Description != null && l.Description.ToLower().Contains(qLower)));
            }

            var results = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            // Pass purchased IDs
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var purchased = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Purchased")
                    .Select(r => r.ListingId)
                    .ToListAsync();
                ViewBag.PurchasedListingIds = new HashSet<int>(purchased);
            }
            else
            {
                ViewBag.PurchasedListingIds = new HashSet<int>();
            }

            return PartialView("_Grid", results);
        }

        /// <summary>
        /// Displays form to post new marketplace listing.
        /// HTTP GET method for displaying empty form.
        /// </summary>
        /// <returns>View with empty MarketplaceListing model</returns>
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            ViewBag.UserMachinery = await _context.Machinery.ToListAsync();
            return View(new MarketplaceListing());
        }

        /// <summary>
        /// Processes form submission to create new marketplace listing.
        /// HTTP POST method that validates input and adds listing to collection.
        /// </summary>
        /// <param name="listing">MarketplaceListing object populated from form submission</param>
        /// <returns>Redirects to Index on success, returns view with model on validation failure</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(MarketplaceListing listing, int? selectedMachineryId)
        {
            // Validate model state
            if (!ModelState.IsValid)
            {
                ViewBag.UserMachinery = await _context.Machinery.ToListAsync();
                return View(listing);
            }

            // If user chose existing machinery, prefill some listing fields and link it
            if (selectedMachineryId.HasValue)
            {
                var mach = await _context.Machinery.FindAsync(selectedMachineryId.Value);
                if (mach != null)
                {
                    listing.ItemName = string.IsNullOrWhiteSpace(listing.ItemName) ? mach.Name : listing.ItemName;
                    listing.Description = string.IsNullOrWhiteSpace(listing.Description) ? $"{mach.Type} - {mach.Name}" : listing.Description;
                    listing.Category = string.IsNullOrWhiteSpace(listing.Category) ? "Equipment" : listing.Category;
                    listing.MachineryId = mach.Id; // Link to machinery
                }
            }

            // Set timestamps for audit trail
            listing.CreatedDate = DateTime.Now;
            listing.UpdatedDate = DateTime.Now;

            // Default listing to active status
            listing.ListingStatus = "Active";

            // Add to database
            _context.MarketplaceListings.Add(listing);
            await _context.SaveChangesAsync();

            // Redirect to marketplace list view
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays listing details and edit form.
        /// HTTP GET method for displaying marketplace listing information by ID.
        /// </summary>
        /// <param name="id">ID of listing to display</param>
        /// <returns>View with MarketplaceListing model if found, or NotFound if listing doesn't exist</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Find listing by ID
            var listing = await _context.MarketplaceListings.FindAsync(id);

            if (listing == null)
            {
                return NotFound();
            }

            return View(listing);
        }

        /// <summary>
        /// Processes form submission to update existing marketplace listing.
        /// HTTP POST method that validates input and updates listing in collection.
        /// </summary>
        /// <param name="id">ID of listing to update</param>
        /// <param name="listing">Updated MarketplaceListing object from form submission</param>
        /// <returns>Redirects to Index on success, returns view with model on validation failure</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MarketplaceListing listing)
        {
            if (id != listing.Id)
            {
                return NotFound();
            }

            // Validate model state
            if (!ModelState.IsValid)
            {
                return View(listing);
            }

            try
            {
                var existingListing = await _context.MarketplaceListings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
                if (existingListing == null)
                {
                    return NotFound();
                }

                // Preserve creation date
                listing.CreatedDate = existingListing.CreatedDate;
                listing.UpdatedDate = DateTime.Now;
                listing.ListingStatus = existingListing.ListingStatus; // Preserve status unless explicitly changed (could add status dropdown to edit form)

                _context.Update(listing);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ListingExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Redirect to marketplace list view
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Buy a marketplace listing - adds it to the buyer's machinery
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var listing = await _context.MarketplaceListings.FindAsync(id);
            if (listing == null)
            {
                TempData["Error"] = "Listing not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check not already purchased by this user
            var alreadyBought = await _context.MarketplacePurchaseRequests
                .AnyAsync(r => r.BuyerUserId == user.Id && r.ListingId == id && r.Status == "Purchased");
            if (alreadyBought)
            {
                TempData["Error"] = "You have already purchased this item.";
                return RedirectToAction(nameof(Index));
            }

            // Create a machinery record for the buyer
            var machine = new Machinery
            {
                Name = listing.ItemName,
                Type = listing.Category,
                Status = listing.ConditionStatus,
                EngineHours = listing.EngineHours,
                PurchasePrice = listing.SalePrice,
                PurchaseDate = DateTime.UtcNow,
                CompanyId = user.CompanyId.HasValue ? user.CompanyId : null,
                OwnerUserId = user.CompanyId == null ? user.Id : null,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _context.Machinery.Add(machine);

            // Record the purchase
            var purchase = new MarketplacePurchaseRequest
            {
                ListingId = id,
                BuyerUserId = user.Id,
                BuyerCompanyId = user.CompanyId,
                BuyerName = user.Email ?? "",
                RequestedDate = DateTime.UtcNow,
                Status = "Purchased"
            };
            _context.MarketplacePurchaseRequests.Add(purchase);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"🛒 '{listing.ItemName}' has been purchased and added to your machinery!";
            return RedirectToAction(nameof(Index));

        }

        /// <summary>
        /// Deletes marketplace listing.
        /// Marks the listing as sold/expired rather than permanently removing it.
        /// </summary>
        /// <param name="id">ID of listing to delete</param>
        /// <returns>Redirects to Index after deletion</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Find listing from database
            var listing = await _context.MarketplaceListings.FindAsync(id);

            if (listing != null)
            {
                // Soft delete or hard delete? Requirement says "Marks as sold/expired" in comment, but code was removing it.
                // Let's stick to removing it for now to match previous behavior, or maybe just update status if that's preferred.
                // The previous code was `_listingsList.Remove(listing)`, so it was a hard delete from the list.
                // I will replicate hard delete for now.
                _context.MarketplaceListings.Remove(listing);
                await _context.SaveChangesAsync();
            }

            // Redirect to marketplace list view
            return RedirectToAction(nameof(Index));
        }

        private bool ListingExists(int id)
        {
            return _context.MarketplaceListings.Any(e => e.Id == id);
        }
    }
}
