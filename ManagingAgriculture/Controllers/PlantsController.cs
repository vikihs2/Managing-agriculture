using Microsoft.AspNetCore.Mvc;
using ManagingAgriculture.Models;
using ManagingAgriculture.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class PlantsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;
        private readonly ManagingAgriculture.Services.CropDataService _cropService;

        public PlantsController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _cropService = new ManagingAgriculture.Services.CropDataService();
        }

        // Helper: get fields for a user (company or personal)
        private async Task<List<Field>> GetUserFields(ApplicationUser user, bool unoccupiedOnly = false)
        {
            IQueryable<Field> query;
            if (user.CompanyId != null)
            {
                query = _context.Fields.Where(f => f.CompanyId == user.CompanyId);
            }
            else
            {
                query = _context.Fields.Where(f => f.OwnerUserId == user.Id);
            }
            if (unoccupiedOnly) query = query.Where(f => !f.IsOccupied);
            return await query.ToListAsync();
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Plant Tracking";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            List<Plant> plants;
            if (user.CompanyId != null)
            {
                plants = await _context.Plants
                    .Include(p => p.Field)
                    .Where(p => p.CompanyId == user.CompanyId && p.Status != "Harvested")
                    .ToListAsync();
            }
            else
            {
                plants = await _context.Plants
                    .Include(p => p.Field)
                    .Where(p => p.OwnerUserId == user.Id && p.Status != "Harvested")
                    .ToListAsync();
            }
            
            return View(plants);
        }

        [HttpGet]
        public async Task<IActionResult> Add()
        {
            ViewData["Title"] = "Add Plant";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Role check - Employees cannot add plants
            if (user.CompanyId != null && User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot add plants.";
                return RedirectToAction("Index");
            }

            // Get available fields - all authenticated users can see fields for their context
            var fields = await GetUserFields(user, unoccupiedOnly: true);

            ViewBag.Fields = fields;
            ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
            var today = System.DateTime.Today;
            return View(new PlantCreateViewModel { PlantedDate = today, CurrentTrackingDate = today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(PlantCreateViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Role check
            if (currentUser.CompanyId != null && User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot add plants.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                ViewBag.Fields = await GetUserFields(currentUser, unoccupiedOnly: true);
                return View(model);
            }

            // Get field
            var field = await _context.Fields.FindAsync(model.FieldId);
            if (field == null)
            {
                ModelState.AddModelError("FieldId", "Field not found. Please add a field first.");
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                ViewBag.Fields = await GetUserFields(currentUser, unoccupiedOnly: true);
                return View(model);
            }

            // Verify field ownership
            if (currentUser.CompanyId != null)
            {
                if (field.CompanyId != currentUser.CompanyId)
                {
                    ModelState.AddModelError("FieldId", "You do not own this field.");
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.Fields = await GetUserFields(currentUser, unoccupiedOnly: true);
                    return View(model);
                }
            }
            else
            {
                if (field.OwnerUserId != currentUser.Id)
                {
                    ModelState.AddModelError("FieldId", "You do not own this field.");
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.Fields = await GetUserFields(currentUser, unoccupiedOnly: true);
                    return View(model);
                }
            }

            // Check if field is still available
            if (field.IsOccupied)
            {
                TempData["Error"] = "This field is already occupied. Choose another field.";
                return RedirectToAction("Add");
            }

            // RESOURCE VALIDATION - Only check Seeds (category = "Seed")
            var seedRequirementKg = GetSeedRequirementKg(model.CropType, field.SizeInDecars);

            if (seedRequirementKg > 0)
            {
                // Find a Seed resource that matches (look for any resource in Seed category)
                var seedResource = await _context.Resources
                    .Where(r =>
                        r.Category == "Seed" &&
                        (r.CompanyId == currentUser.CompanyId || (r.CompanyId == null && r.OwnerUserId == currentUser.Id)))
                    .FirstOrDefaultAsync();

                // Convert requirement to the unit stored in the resource
                decimal requiredAmount = ConvertSeedRequirement(seedRequirementKg, seedResource?.Unit ?? "kg");

                if (seedResource == null || seedResource.Quantity < requiredAmount)
                {
                    decimal have = seedResource?.Quantity ?? 0;
                    string unit = seedResource?.Unit ?? "kg";
                    TempData["Error"] = $"Not enough seeds to plant {model.CropType} on a {field.SizeInDecars} decar field. " +
                        $"Need {requiredAmount:N1} {unit} of seeds, but only have {have:N1} {unit}. " +
                        $"Please add more seeds in the Resources section (Category: Seed).";
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.Fields = await GetUserFields(currentUser, unoccupiedOnly: true);
                    return View(model);
                }

                // Deduct seeds from resource
                seedResource.Quantity -= requiredAmount;
                seedResource.UpdatedDate = System.DateTime.Now;
                _context.Resources.Update(seedResource);
            }

            // Use CurrentTrackingDate if provided, otherwise use today
            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;

            // Calculate Growth %
            int growthPercent = _cropService.CalculateGrowthPercentage(
                model.PlantedDate,
                trackingDate,
                model.CropType,
                field.SoilType ?? "",
                field.AverageTemperatureCelsius,
                model.IsIndoor,
                model.WateringFrequencyDays ?? 0,
                field.SunlightExposure ?? ""
            );

            var plant = new Plant
            {
                Name = model.Name,
                PlantType = model.CropType,
                PlantedDate = model.PlantedDate,
                CurrentTrackingDate = trackingDate,
                GrowthStagePercent = growthPercent,
                NextTask = model.NextTask,
                Notes = model.Notes,
                FieldId = model.FieldId,
                IsIndoor = model.IsIndoor,
                WateringFrequencyDays = model.WateringFrequencyDays,
                CreatedDate = System.DateTime.UtcNow,
                UpdatedDate = System.DateTime.UtcNow,
                Status = "Active",
                CompanyId = currentUser.CompanyId,
                OwnerUserId = currentUser.CompanyId == null ? currentUser.Id : null
            };

            _context.Plants.Add(plant);
            await _context.SaveChangesAsync();

            // Update field to occupied
            field.IsOccupied = true;
            field.CurrentPlantId = plant.Id;
            _context.Fields.Update(field);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Plant '{plant.Name}' added successfully!";
            return RedirectToAction("Index");
        }


        // Helper method to get required resources for a crop type and field size
        private List<(string Name, decimal Quantity, string Unit)> GetRequiredResources(string cropType, decimal fieldSizeDecars)
        {
            var seedRequirements = new Dictionary<string, decimal>
            {
                // Grains
                { "Wheat", 200 }, { "Corn (Maize)", 25 }, { "Rice", 150 }, { "Barley", 180 },
                { "Oats", 150 }, { "Rye", 180 }, { "Sorghum", 10 }, { "Millet", 15 },
                // Root & Tuber
                { "Potato", 200 }, { "Sweet Potato", 250 }, { "Carrot", 8 }, { "Beetroot", 20 },
                { "Turnip", 5 }, { "Radish", 8 }, { "Cassava", 20 },
                // Vegetables
                { "Tomato", 0.5m }, { "Cucumber", 2 }, { "Bell Pepper", 0.3m }, { "Chili Pepper", 0.3m },
                { "Eggplant", 0.3m }, { "Onion", 20 }, { "Garlic", 200 }, { "Lettuce", 1 },
                { "Spinach", 2 }, { "Cabbage", 0.5m }, { "Broccoli", 0.3m }, { "Cauliflower", 0.3m },
                { "Zucchini", 2 },
                // Legumes
                { "Beans (Green Beans)", 15 }, { "Peas", 80 }, { "Lentils", 100 },
                { "Chickpeas", 100 }, { "Soybean", 80 },
                // Fruits
                { "Strawberry", 1 }, { "Watermelon", 3 }, { "Melon", 2 }, { "Pumpkin", 3 }, { "Squash", 3 },
                // Industrial
                { "Sunflower", 20 }, { "Rapeseed (Canola)", 10 }, { "Cotton", 10 },
                { "Sugar Beet", 25 }, { "Sugarcane", 30 },
                // Herbs
                { "Basil", 0.1m }, { "Parsley", 0.2m }, { "Dill", 0.2m }, { "Mint", 0.5m },
                { "Oregano", 0.2m }, { "Thyme", 0.1m },
                // Perennial
                { "Alfalfa", 30 }, { "Clover", 25 }, { "Tobacco", 0.1m }
            };

            var resources = new List<(string Name, decimal Quantity, string Unit)>();

            if (seedRequirements.TryGetValue(cropType, out var seedPerDecar))
            {
                resources.Add(($"{cropType} Seeds", seedPerDecar * fieldSizeDecars, "kg"));
            }

            // All crops need fertilizer (generic requirement: 50kg per decar)
            resources.Add(("Fertilizer NPK 20-20-20", 50 * fieldSizeDecars, "kg"));

            return resources;
        }

        /// <summary>
        /// Returns the required seed quantity in kg per decar for a given crop type.
        /// Multiplied by field size to get total seed needed.
        /// </summary>
        private decimal GetSeedRequirementKg(string cropType, decimal fieldSizeDecars)
        {
            var seedPerDecar = new Dictionary<string, decimal>
            {
                // Grains
                { "Wheat", 200 }, { "Corn (Maize)", 25 }, { "Rice", 150 }, { "Barley", 180 },
                { "Oats", 150 }, { "Rye", 180 }, { "Sorghum", 10 }, { "Millet", 15 },
                // Root & Tuber
                { "Potato", 200 }, { "Sweet Potato", 250 }, { "Carrot", 8 }, { "Beetroot", 20 },
                { "Turnip", 5 }, { "Radish", 8 }, { "Cassava", 20 },
                // Vegetables
                { "Tomato", 0.5m }, { "Cucumber", 2 }, { "Bell Pepper", 0.3m }, { "Chili Pepper", 0.3m },
                { "Eggplant", 0.3m }, { "Onion", 20 }, { "Garlic", 200 }, { "Lettuce", 1 },
                { "Spinach", 2 }, { "Cabbage", 0.5m }, { "Broccoli", 0.3m }, { "Cauliflower", 0.3m },
                { "Zucchini", 2 },
                // Legumes
                { "Beans (Green Beans)", 15 }, { "Peas", 80 }, { "Lentils", 100 },
                { "Chickpeas", 100 }, { "Soybean", 80 },
                // Fruits
                { "Strawberry", 1 }, { "Watermelon", 3 }, { "Melon", 2 }, { "Pumpkin", 3 }, { "Squash", 3 },
                // Industrial
                { "Sunflower", 20 }, { "Rapeseed (Canola)", 10 }, { "Cotton", 10 },
                { "Sugar Beet", 25 }, { "Sugarcane", 30 },
                // Herbs
                { "Basil", 0.1m }, { "Parsley", 0.2m }, { "Dill", 0.2m }, { "Mint", 0.5m },
                { "Oregano", 0.2m }, { "Thyme", 0.1m },
                // Perennial
                { "Alfalfa", 30 }, { "Clover", 25 }, { "Tobacco", 0.1m }
            };

            if (seedPerDecar.TryGetValue(cropType, out var perDecar))
                return perDecar * fieldSizeDecars;

            // Default: 10 kg per decar for unknown crops
            return 10 * fieldSizeDecars;
        }

        /// <summary>
        /// Converts seed requirement from kg to the unit stored in the resource.
        /// </summary>
        private decimal ConvertSeedRequirement(decimal requirementKg, string unit)
        {
            return unit switch
            {
                "g"     => requirementKg * 1000m,
                "tons"  => requirementKg / 1000m,
                "bags"  => requirementKg / 25m,  // assume 25 kg per bag
                "boxes" => requirementKg / 10m,  // assume 10 kg per box
                "pieces" or "units" => requirementKg * 10m, // rough: seed pieces
                _       => requirementKg // kg, liters, ml, m³ etc. – treat as 1:1
            };
        }



        // Helper: calculate estimated yield in kg based on crop type and field size
        private decimal CalculateYield(string cropType, decimal fieldSizeDecars)
        {
            // Typical yield per decar in kg
            var yieldPerDecar = new Dictionary<string, decimal>
            {
                { "Wheat", 350 }, { "Corn (Maize)", 800 }, { "Rice", 600 }, { "Barley", 250 },
                { "Oats", 200 }, { "Rye", 200 }, { "Sorghum", 400 }, { "Millet", 150 },
                { "Potato", 2500 }, { "Sweet Potato", 1500 }, { "Carrot", 2000 }, { "Beetroot", 2000 },
                { "Turnip", 1500 }, { "Radish", 1000 }, { "Cassava", 1500 },
                { "Tomato", 4000 }, { "Cucumber", 3000 }, { "Bell Pepper", 1500 }, { "Chili Pepper", 800 },
                { "Eggplant", 2000 }, { "Onion", 2500 }, { "Garlic", 1000 }, { "Lettuce", 3000 },
                { "Spinach", 1500 }, { "Cabbage", 4000 }, { "Broccoli", 1200 }, { "Cauliflower", 1000 },
                { "Zucchini", 3000 },
                { "Beans (Green Beans)", 500 }, { "Peas", 300 }, { "Lentils", 200 },
                { "Chickpeas", 200 }, { "Soybean", 250 },
                { "Strawberry", 1000 }, { "Watermelon", 4000 }, { "Melon", 3000 }, { "Pumpkin", 2000 }, { "Squash", 1500 },
                { "Sunflower", 250 }, { "Rapeseed (Canola)", 200 }, { "Cotton", 300 },
                { "Sugar Beet", 5000 }, { "Sugarcane", 8000 },
                { "Basil", 400 }, { "Parsley", 600 }, { "Dill", 400 }, { "Mint", 500 },
                { "Oregano", 200 }, { "Thyme", 150 },
                { "Alfalfa", 800 }, { "Clover", 600 }, { "Tobacco", 300 }
            };

            if (yieldPerDecar.TryGetValue(cropType, out var yield))
                return yield * fieldSizeDecars;

            return 500 * fieldSizeDecars; // default
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.Include(p => p.Field).FirstOrDefaultAsync(p => p.Id == id);

            if (plant == null) return NotFound();

            if (user.CompanyId != null)
            {
                if (plant.CompanyId != user.CompanyId) return Forbid();
                
                // Role Restrictions - Only Boss and Manager can edit plants
                if (User.IsInRole("Employee")) 
                {
                    TempData["Error"] = "Employees cannot edit plants.";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                if (plant.OwnerUserId != user.Id) return Forbid();
            }

            var model = new PlantCreateViewModel
            {
                Name = plant.Name,
                CropType = plant.PlantType,
                PlantedDate = plant.PlantedDate,
                CurrentTrackingDate = plant.CurrentTrackingDate,
                GrowthStage = plant.GrowthStagePercent,
                FieldId = plant.FieldId ?? 0,
                NextTask = plant.NextTask,
                Notes = plant.Notes,
                IsIndoor = plant.IsIndoor,
                WateringFrequencyDays = plant.WateringFrequencyDays
            };
            
            // Load all fields (include current one even if occupied)
            ViewBag.Fields = await GetUserFields(user);
            ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
            ViewBag.Id = plant.Id;
            ViewBag.CropTypeLocked = true;
            
            // Get plant health status
            var healthStatus = _cropService.GetPlantStatus(plant.GrowthStagePercent, plant.PlantedDate, System.DateTime.Today);
            ViewBag.PlantStatus = healthStatus;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlantCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Role restrictions
            if (user.CompanyId != null && User.IsInRole("Employee"))
            {
                TempData["Error"] = "Employees cannot edit plants.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Fields = await GetUserFields(user);
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                ViewBag.CropTypeLocked = true;
                ViewBag.Id = id;
                return View(model);
            }

            var plant = await _context.Plants.Include(p => p.Field).FirstOrDefaultAsync(p => p.Id == id);

            if (plant == null) return NotFound();

            // Authorization
            if (user.CompanyId != null)
            {
                if (plant.CompanyId != user.CompanyId) return Forbid();
            }
            else
            {
                if (plant.OwnerUserId != user.Id) return Forbid();
            }
            
            // CROP TYPE IS LOCKED
            if (plant.PlantType != model.CropType)
            {
                model.CropType = plant.PlantType; // Force original
            }

            // Handle Field change
            if (plant.FieldId != model.FieldId)
            {
                var newField = await _context.Fields.FindAsync(model.FieldId);
                if (newField == null)
                {
                    ModelState.AddModelError("FieldId", "Field not found.");
                    ViewBag.Fields = await GetUserFields(user);
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.CropTypeLocked = true;
                    ViewBag.Id = id;
                    return View(model);
                }
                if (newField.IsOccupied)
                {
                    ModelState.AddModelError("FieldId", "The selected field is already occupied by another plant.");
                    ViewBag.Fields = await GetUserFields(user);
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.CropTypeLocked = true;
                    ViewBag.Id = id;
                    return View(model);
                }

                // Free old field
                if (plant.FieldId.HasValue)
                {
                    var oldField = await _context.Fields.FindAsync(plant.FieldId);
                    if (oldField != null)
                    {
                        oldField.IsOccupied = false;
                        oldField.CurrentPlantId = null;
                        _context.Update(oldField);
                    }
                }

                plant.FieldId = model.FieldId;
                newField.IsOccupied = true;
                _context.Update(newField);
                plant.Field = newField;
            }

            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;
            
            int growthPercent = _cropService.CalculateGrowthPercentage(
                model.PlantedDate,
                trackingDate,
                plant.PlantType,
                plant.Field?.SoilType ?? "",
                plant.Field?.AverageTemperatureCelsius,
                model.IsIndoor,
                model.WateringFrequencyDays ?? 0,
                plant.Field?.SunlightExposure ?? ""
            );

            plant.Name = model.Name;
            plant.PlantedDate = model.PlantedDate;
            plant.CurrentTrackingDate = trackingDate;
            plant.GrowthStagePercent = growthPercent;
            plant.NextTask = model.NextTask;
            plant.Notes = model.Notes;
            plant.IsIndoor = model.IsIndoor;
            plant.WateringFrequencyDays = model.WateringFrequencyDays;
            plant.UpdatedDate = System.DateTime.UtcNow;

            _context.Update(plant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Plant updated successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.FindAsync(id);
            if (plant != null)
            {
                if (user.CompanyId != null)
                {
                    if (plant.CompanyId != user.CompanyId) return Forbid();
                    
                    // Only Boss can delete company plants; Employee/Manager cannot
                    if (User.IsInRole("Employee") || User.IsInRole("Manager")) 
                    {
                        TempData["Error"] = "Only the Boss can delete plants.";
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    // Non-company users (User, SystemAdmin, ITSupport) can delete their own plants
                    if (plant.OwnerUserId != user.Id) return Forbid();
                }

                // Free the field
                if (plant.FieldId.HasValue)
                {
                    var field = await _context.Fields.FindAsync(plant.FieldId);
                    if (field != null)
                    {
                        field.IsOccupied = false;
                        field.CurrentPlantId = null;
                        _context.Fields.Update(field);
                    }
                }

                _context.Plants.Remove(plant);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Harvest(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.Include(p => p.Field).FirstOrDefaultAsync(p => p.Id == id);
            if (plant == null) return NotFound();

            // Authorization Check - anyone in the company can harvest
            if (user.CompanyId != null)
            {
                if (plant.CompanyId != user.CompanyId) return Forbid();
            }
            else
            {
                if (plant.OwnerUserId != user.Id) return Forbid();
            }

            // Can only harvest at 100%
            if (plant.GrowthStagePercent < 100)
            {
                TempData["Error"] = $"Cannot harvest yet! Plant is only at {plant.GrowthStagePercent}% growth. Must be 100%.";
                return RedirectToAction("Index");
            }

            // Calculate yield
            var fieldSize = plant.Field?.SizeInDecars ?? 1;
            var yieldKg = CalculateYield(plant.PlantType, fieldSize);

            // Create harvest record
            var harvestRecord = new HarvestRecord
            {
                CompanyId = user.CompanyId,
                OwnerUserId = user.CompanyId == null ? user.Id : null,
                PlantName = plant.Name,
                PlantType = plant.PlantType,
                FieldName = plant.Field?.Name,
                FieldSizeDecars = fieldSize,
                EstimatedYieldKg = yieldKg,
                HarvestedDate = System.DateTime.UtcNow,
                HarvestedByUserId = user.Id
            };
            _context.HarvestRecords.Add(harvestRecord);

            // Free the field
            if (plant.FieldId.HasValue)
            {
                var field = await _context.Fields.FindAsync(plant.FieldId);
                if (field != null)
                {
                    field.IsOccupied = false;
                    field.CurrentPlantId = null;
                    _context.Fields.Update(field);
                }
            }

            // Remove plant from tracking (it disappears from plant tracking)
            _context.Plants.Remove(plant);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"🌾 Harvested {plant.Name}! Estimated yield: {yieldKg:N0} kg of {plant.PlantType}.";
            return RedirectToAction("Index");
        }
    }
}
