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

        /// <summary>Returns true if the user is allowed to transact in the marketplace (buy/sell).</summary>
        private bool CanTransact(ApplicationUser user)
        {
            // Company users: only Boss can transact; Manager and Employee are view-only
            if (user.CompanyId != null)
                return User.IsInRole("Boss");
            // Non-company users (User, SystemAdmin, ITSupport) can always transact
            return true;
        }

        // Helper: get friendly display name (same logic as _Layout)
        private string GetDisplayName(ApplicationUser user)
        {
            var email = user.Email ?? "";
            if (email.Contains("@"))
            {
                var raw = email.Split('@')[0];
                return raw.Length > 0 ? char.ToUpper(raw[0]) + raw.Substring(1) : raw;
            }
            return email;
        }

        public async Task<IActionResult> Index(string? q = null)
        {
            ViewData["Title"] = "Marketplace";

            var query = _context.MarketplaceListings.AsQueryable()
                .Where(l => l.ListingStatus == "Active");

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(l => l.ItemName.ToLower().Contains(qLower) ||
                    (l.Description != null && l.Description.ToLower().Contains(qLower)));
            }

            var results = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            ViewBag.Query = q ?? string.Empty;

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // IDs where user's request is approved (bought)
                var approvedIds = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Approved")
                    .Select(r => r.ListingId)
                    .ToListAsync();
                ViewBag.ApprovedListingIds = new HashSet<int>(approvedIds);

                // IDs where user sent a pending request
                var pendingIds = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Pending")
                    .Select(r => r.ListingId)
                    .ToListAsync();
                ViewBag.PendingListingIds = new HashSet<int>(pendingIds);

                ViewBag.CurrentUserId = user.Id;
                ViewBag.CanTransact = CanTransact(user);
            }
            else
            {
                ViewBag.ApprovedListingIds = new HashSet<int>();
                ViewBag.PendingListingIds = new HashSet<int>();
                ViewBag.CurrentUserId = "";
                ViewBag.CanTransact = false;
            }

            return View(results);
        }

        [HttpGet]
        public async Task<IActionResult> Filter(string? q = null)
        {
            var query = _context.MarketplaceListings.AsQueryable()
                .Where(l => l.ListingStatus == "Active");

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(l => l.ItemName.ToLower().Contains(qLower) ||
                    (l.Description != null && l.Description.ToLower().Contains(qLower)));
            }

            var results = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var approvedIds = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Approved")
                    .Select(r => r.ListingId).ToListAsync();
                ViewBag.ApprovedListingIds = new HashSet<int>(approvedIds);

                var pendingIds = await _context.MarketplacePurchaseRequests
                    .Where(r => r.BuyerUserId == user.Id && r.Status == "Pending")
                    .Select(r => r.ListingId).ToListAsync();
                ViewBag.PendingListingIds = new HashSet<int>(pendingIds);

                ViewBag.CurrentUserId = user.Id;
                ViewBag.CanTransact = CanTransact(user);
            }
            else
            {
                ViewBag.ApprovedListingIds = new HashSet<int>();
                ViewBag.PendingListingIds = new HashSet<int>();
                ViewBag.CurrentUserId = "";
                ViewBag.CanTransact = false;
            }

            return PartialView("_Grid", results);
        }

        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!CanTransact(user))
            {
                TempData["Error"] = "Only the Boss can post listings in the marketplace.";
                return RedirectToAction(nameof(Index));
            }

            // Load machinery owned by this user (or company)
            List<Machinery> machinery;
            if (user.CompanyId != null)
                machinery = await _context.Machinery.Where(m => m.CompanyId == user.CompanyId).ToListAsync();
            else
                machinery = await _context.Machinery.Where(m => m.OwnerUserId == user.Id).ToListAsync();

            ViewBag.UserMachinery = machinery;

            // Pre-fill seller name from welcome name
            var newListing = new MarketplaceListing
            {
                SellerName = GetDisplayName(user),
                SellerUserId = user.Id,
                Category = "Equipment"
            };
            return View(newListing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(MarketplaceListing listing, int? selectedMachineryId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!CanTransact(user))
            {
                TempData["Error"] = "Only the Boss can post listings in the marketplace.";
                return RedirectToAction(nameof(Index));
            }

            // Force seller identity — always use the logged-in user's display name and ID
            listing.SellerUserId = user.Id;
            listing.SellerName = GetDisplayName(user);
            listing.Category = "Equipment"; // Only machinery sold here

            // Remove internal or auto-filled fields from ModelState validation
            ModelState.Remove("SellerName");
            ModelState.Remove("Category");
            ModelState.Remove("ConditionStatus"); // Auto-filled physically or not required if rent-only

            if (!ModelState.IsValid)
            {
                List<Machinery> mach;
                if (user.CompanyId != null)
                    mach = await _context.Machinery.Where(m => m.CompanyId == user.CompanyId).ToListAsync();
                else
                    mach = await _context.Machinery.Where(m => m.OwnerUserId == user.Id).ToListAsync();
                ViewBag.UserMachinery = mach;
                return View(listing);
            }

            if (selectedMachineryId.HasValue)
            {
                var mach = await _context.Machinery.FindAsync(selectedMachineryId.Value);
                if (mach != null)
                {
                    listing.ItemName = string.IsNullOrWhiteSpace(listing.ItemName) ? mach.Name : listing.ItemName;
                    listing.Description = string.IsNullOrWhiteSpace(listing.Description) ? $"{mach.Type} - {mach.Name}" : listing.Description;
                    listing.MachineryId = mach.Id;
                }
            }

            listing.CreatedDate = DateTime.Now;
            listing.UpdatedDate = DateTime.Now;
            listing.ListingStatus = "Active";

            _context.MarketplaceListings.Add(listing);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Listing posted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var listing = await _context.MarketplaceListings.FindAsync(id);
            if (listing == null) return NotFound();

            // Only the seller can edit
            if (listing.SellerUserId != user.Id)
            {
                TempData["Error"] = "You can only edit your own listings.";
                return RedirectToAction(nameof(Index));
            }

            return View(listing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MarketplaceListing listing)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (id != listing.Id) return NotFound();

            var existing = await _context.MarketplaceListings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (existing == null) return NotFound();

            // Only the seller can edit
            if (existing.SellerUserId != user.Id)
            {
                TempData["Error"] = "You can only edit your own listings.";
                return RedirectToAction(nameof(Index));
            }

            // Force seller identity
            listing.SellerUserId = existing.SellerUserId;
            listing.SellerName = existing.SellerName;
            listing.Category = "Equipment";
            ModelState.Remove("SellerName");

            if (!ModelState.IsValid) return View(listing);

            try
            {
                listing.CreatedDate = existing.CreatedDate;
                listing.UpdatedDate = DateTime.Now;
                listing.ListingStatus = existing.ListingStatus;
                _context.Update(listing);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.MarketplaceListings.Any(e => e.Id == id)) return NotFound();
                throw;
            }

            TempData["Success"] = "Listing updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Send a purchase request to the seller.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestToBuy(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!CanTransact(user))
            {
                TempData["Error"] = "Only the Boss can buy from the marketplace.";
                return RedirectToAction(nameof(Index));
            }

            var listing = await _context.MarketplaceListings.FindAsync(id);
            if (listing == null || listing.ListingStatus != "Active")
            {
                TempData["Error"] = "Listing not found or no longer available.";
                return RedirectToAction(nameof(Index));
            }

            // Can't buy your own listing
            if (listing.SellerUserId == user.Id)
            {
                TempData["Error"] = "You cannot buy your own listing.";
                return RedirectToAction(nameof(Index));
            }

            // Check for existing pending/approved request from this user
            var existingRequest = await _context.MarketplacePurchaseRequests
                .AnyAsync(r => r.ListingId == id && r.BuyerUserId == user.Id && (r.Status == "Pending" || r.Status == "Approved"));
            if (existingRequest)
            {
                TempData["Error"] = "You have already sent a request for this listing.";
                return RedirectToAction(nameof(Index));
            }

            var request = new MarketplacePurchaseRequest
            {
                ListingId = id,
                BuyerUserId = user.Id,
                BuyerCompanyId = user.CompanyId,
                BuyerName = GetDisplayName(user),
                RequestedDate = DateTime.UtcNow,
                Status = "Pending"
            };
            _context.MarketplacePurchaseRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Purchase request sent to the seller!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Seller views all pending requests for their listings.</summary>
        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!CanTransact(user))
            {
                TempData["Error"] = "Only the Boss can manage marketplace requests.";
                return RedirectToAction(nameof(Index));
            }

            var requests = await _context.MarketplacePurchaseRequests
                .Include(r => r.Listing)
                .Include(r => r.BuyerUser)
                .Where(r => r.Listing!.SellerUserId == user.Id && r.Status == "Pending")
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            return View(requests);
        }

        /// <summary>Seller approves a purchase request — transfers machinery to buyer, marks listing as Sold.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int requestId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var request = await _context.MarketplacePurchaseRequests
                .Include(r => r.Listing)
                .Include(r => r.BuyerUser)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.Listing == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(MyRequests));
            }

            // Only the seller can approve
            if (request.Listing.SellerUserId != user.Id)
            {
                TempData["Error"] = "You can only approve requests for your own listings.";
                return RedirectToAction(nameof(MyRequests));
            }

            var buyer = request.BuyerUser;
            if (buyer == null) buyer = await _userManager.FindByIdAsync(request.BuyerUserId);

            // Create machinery record for the buyer
            var machine = new Machinery
            {
                Name = request.Listing.ItemName,
                Type = request.Listing.Category,
                Status = request.Listing.ConditionStatus,
                EngineHours = request.Listing.EngineHours,
                PurchasePrice = request.Listing.SalePrice,
                PurchaseDate = DateTime.UtcNow,
                CompanyId = buyer?.CompanyId,
                OwnerUserId = buyer?.CompanyId == null ? request.BuyerUserId : null,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _context.Machinery.Add(machine);

            // Approve this request
            request.Status = "Approved";

            // Reject all other pending requests for the same listing
            var otherRequests = await _context.MarketplacePurchaseRequests
                .Where(r => r.ListingId == request.ListingId && r.Id != requestId && r.Status == "Pending")
                .ToListAsync();
            foreach (var r in otherRequests)
                r.Status = "Rejected";

            // Remove listing from marketplace (mark as Sold)
            request.Listing.ListingStatus = "Sold";
            request.Listing.UpdatedDate = DateTime.Now;
            _context.Update(request.Listing);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Purchase approved! The machinery has been transferred to {buyer?.Email ?? "buyer"}.";
            return RedirectToAction(nameof(MyRequests));
        }

        /// <summary>Seller rejects a purchase request.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int requestId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var request = await _context.MarketplacePurchaseRequests
                .Include(r => r.Listing)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.Listing?.SellerUserId != user.Id)
            {
                TempData["Error"] = "Request not found or unauthorized.";
                return RedirectToAction(nameof(MyRequests));
            }

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(MyRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var listing = await _context.MarketplaceListings.FindAsync(id);
            if (listing == null) return RedirectToAction(nameof(Index));

            // Only the seller can delete
            if (listing.SellerUserId != user.Id)
            {
                TempData["Error"] = "You can only delete your own listings.";
                return RedirectToAction(nameof(Index));
            }

            _context.MarketplaceListings.Remove(listing);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Listing deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
