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
        // In a real app we would inject the service, but static for now is fine or we can add to Program.cs
        private readonly ManagingAgriculture.Services.CropDataService _cropService;

        public PlantsController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _cropService = new ManagingAgriculture.Services.CropDataService();
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
                    .Where(p => p.CompanyId == user.CompanyId || (p.CompanyId == null && p.OwnerUserId == user.Id))
                    .ToListAsync();
            }
            else
            {
                plants = await _context.Plants
                    .Include(p => p.Field)
                    .Where(p => p.OwnerUserId == user.Id)
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
                return Forbid();
            }

            // Get available fields (not occupied)
            var fields = await _context.Fields
                .Where(f => (f.CompanyId == user.CompanyId || (f.CompanyId == null && f.OwnerUserId == user.Id)) && !f.IsOccupied)
                .ToListAsync();

            ViewBag.Fields = fields;
            ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
            var today = System.DateTime.Today;
            return View(new PlantCreateViewModel { PlantedDate = today, CurrentTrackingDate = today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(PlantCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var fields = await _context.Fields
                        .Where(f => (f.CompanyId == user.CompanyId || (f.CompanyId == null && f.OwnerUserId == user.Id)) && !f.IsOccupied)
                        .ToListAsync();
                    ViewBag.Fields = fields;
                }
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Get field
            var field = await _context.Fields.FindAsync(model.FieldId);
            if (field == null) return NotFound("Field not found");

            // Verify field ownership
            if (currentUser.CompanyId != null)
            {
                if (field.CompanyId != currentUser.CompanyId) return Forbid();
            }
            else
            {
                if (field.OwnerUserId != currentUser.Id) return Forbid();
            }

            // Check if field is still available
            if (field.IsOccupied)
            {
                TempData["Error"] = "This field is already occupied. Choose another field.";
                return RedirectToAction("Add");
            }

            // RESOURCE VALIDATION
            var requiredResources = GetRequiredResources(model.CropType, field.SizeInDecars);
            var insufficientResources = new List<string>();

            foreach (var requiredResource in requiredResources)
            {
                var resource = await _context.Resources
                    .FirstOrDefaultAsync(r => 
                        r.Name.ToLower() == requiredResource.Name.ToLower() &&
                        (r.CompanyId == currentUser.CompanyId || (r.CompanyId == null && r.OwnerUserId == currentUser.Id)));

                if (resource == null || resource.Quantity < requiredResource.Quantity)
                {
                    insufficientResources.Add($"{requiredResource.Name} (Need {requiredResource.Quantity}{requiredResource.Unit}, Have {resource?.Quantity ?? 0}{requiredResource.Unit})");
                }
            }

            if (insufficientResources.Any())
            {
                TempData["Error"] = "Not enough resources to plant this crop on selected field: " + string.Join(", ", insufficientResources);
                return RedirectToAction("Add");
            }

            // Use CurrentTrackingDate if provided, otherwise use today
            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;

            // Calculate Growth % using the new algorithm
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
                CompanyId = currentUser.CompanyId,
                OwnerUserId = currentUser.CompanyId == null ? currentUser.Id : null
            };

            // Save plant first to get the Id
            _context.Plants.Add(plant);
            await _context.SaveChangesAsync();

            // Update field to occupied
            field.IsOccupied = true;
            field.CurrentPlantId = plant.Id;
            _context.Fields.Update(field);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // Helper method to get required resources for a crop type and field size
        private List<(string Name, decimal Quantity, string Unit)> GetRequiredResources(string cropType, decimal fieldSizeDecars)
        {
            // Seed requirements per decar for different crops (in kg)
            var seedRequirements = new Dictionary<string, decimal>
            {
                // Grains
                { "Wheat", 200 },
                { "Corn (Maize)", 25 },
                { "Rice", 150 },
                { "Barley", 180 },
                { "Oats", 150 },
                { "Rye", 180 },
                { "Sorghum", 10 },
                { "Millet", 15 },
                
                // Root & Tuber
                { "Potato", 200 },
                { "Sweet Potato", 250 },
                { "Carrot", 8 },
                { "Beetroot", 20 },
                { "Turnip", 5 },
                { "Radish", 8 },
                { "Cassava", 20 },
                
                // Vegetables
                { "Tomato", 0.5m },
                { "Cucumber", 2 },
                { "Bell Pepper", 0.3m },
                { "Chili Pepper", 0.3m },
                { "Eggplant", 0.3m },
                { "Onion", 20 },
                { "Garlic", 200 },
                { "Lettuce", 1 },
                { "Spinach", 2 },
                { "Cabbage", 0.5m },
                { "Broccoli", 0.3m },
                { "Cauliflower", 0.3m },
                { "Zucchini", 2 },
                
                // Legumes
                { "Beans (Green Beans)", 15 },
                { "Peas", 80 },
                { "Lentils", 100 },
                { "Chickpeas", 100 },
                { "Soybean", 80 },
                
                // Fruits
                { "Strawberry", 1 },
                { "Watermelon", 3 },
                { "Melon", 2 },
                { "Pumpkin", 3 },
                { "Squash", 3 },
                
                // Industrial
                { "Sunflower", 20 },
                { "Rapeseed (Canola)", 10 },
                { "Cotton", 10 },
                { "Sugar Beet", 25 },
                { "Sugarcane", 30 },
                
                // Herbs
                { "Basil", 0.1m },
                { "Parsley", 0.2m },
                { "Dill", 0.2m },
                { "Mint", 0.5m },
                { "Oregano", 0.2m },
                { "Thyme", 0.1m },
                
                // Perennial
                { "Alfalfa", 30 },
                { "Clover", 25 },
                { "Tobacco", 0.1m }
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

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.Include(p => p.Field).FirstOrDefaultAsync(p => p.Id == id);

            if (plant == null) return NotFound();

            if (user.CompanyId != null)
            {
                // Company Logic
                if (plant.CompanyId != user.CompanyId) return Forbid();
                
                // Role Restrictions - Only Boss and Manager can edit plants
                if (User.IsInRole("Employee")) 
                {
                    return Forbid();
                }
            }
            else
            {
                // Personal Logic - Owner can do whatever
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
            
            // Load available fields - include current field even if occupied
            var fields = await _context.Fields
                .Where(f => (f.CompanyId == user.CompanyId || (f.CompanyId == null && f.OwnerUserId == user.Id)))
                .ToListAsync();
            ViewBag.Fields = fields;
            
            ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
            ViewBag.Id = plant.Id;
            ViewBag.CropTypeLocked = true; // Prevent editing crop type
            ViewBag.PlantStatus = plant.Status;
            
            // Get crop recommendations
            if (!string.IsNullOrEmpty(plant.PlantType))
            {
                var (recSoil, recSun, recTemp, recWater) = _cropService.GetCropRecommendations(plant.PlantType);
                ViewBag.RecommendedSoil = recSoil;
                ViewBag.RecommendedSunlight = recSun;
                ViewBag.RecommendedTemperature = recTemp;
                ViewBag.RecommendedWater = recWater;
            }
            
            // Get plant health status
            var healthStatus = _cropService.GetPlantStatus(plant.GrowthStagePercent, plant.PlantedDate, System.DateTime.Today);
            ViewBag.PlantStatus = healthStatus;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlantCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    ViewBag.Fields = await _context.Fields
                        .Where(f => (f.CompanyId == currentUser.CompanyId || (f.CompanyId == null && f.OwnerUserId == currentUser.Id)))
                        .ToListAsync();
                }
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                ViewBag.CropTypeLocked = true;
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.Include(p => p.Field).FirstOrDefaultAsync(p => p.Id == id);

            if (plant == null) return NotFound();

             // Authorization
            if (user.CompanyId != null)
            {
                if (plant.CompanyId != user.CompanyId) return Forbid();
                if (User.IsInRole("Employee")) 
                {
                    return Forbid();
                }
            }
            else
            {
                if (plant.OwnerUserId != user.Id) return Forbid();
            }
            
            // CROP TYPE IS LOCKED - CANNOT BE CHANGED AFTER CREATION
            if (plant.PlantType != model.CropType)
            {
                ModelState.AddModelError("CropType", "Crop type cannot be changed after planting.");
                ViewBag.Fields = await _context.Fields
                    .Where(f => (f.CompanyId == user.CompanyId || (f.CompanyId == null && f.OwnerUserId == user.Id)))
                    .ToListAsync();
                ViewBag.CropTypeLocked = true;
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                return View(model);
            }

            // Handle Field change
            if (plant.FieldId != model.FieldId)
            {
                var newField = await _context.Fields.FindAsync(model.FieldId);
                if (newField == null) return NotFound("Field not found");
                if (newField.IsOccupied)
                {
                    ModelState.AddModelError("FieldId", "The selected field is already occupied by another plant.");
                    ViewBag.Fields = await _context.Fields
                        .Where(f => (f.CompanyId == user.CompanyId || (f.CompanyId == null && f.OwnerUserId == user.Id)))
                        .ToListAsync();
                    ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                    ViewBag.CropTypeLocked = true;
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

                // Occupy new field
                plant.FieldId = model.FieldId;
                newField.IsOccupied = true;
                // Currently EF couldn't set CurrentPlantId before saving plant, we will save it after update.
                _context.Update(newField);
                
                // Temporarily assign new field to plant for growth calculation below
                plant.Field = newField;
            }

            // Use CurrentTrackingDate if provided, otherwise use today
            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;
            
            // Calculate growth % using the new algorithm
            int growthPercent = _cropService.CalculateGrowthPercentage(
                model.PlantedDate,
                trackingDate,
                model.CropType,
                plant.Field?.SoilType ?? "",
                plant.Field?.AverageTemperatureCelsius,
                model.IsIndoor,
                model.WateringFrequencyDays ?? 0,
                plant.Field?.SunlightExposure ?? ""
            );

            plant.Name = model.Name;
            // plant.PlantType = model.CropType; // LOCKED
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
                 // Authorization Check
                if (user.CompanyId != null)
                {
                    if (plant.CompanyId != user.CompanyId) return Forbid();
                    
                     if (User.IsInRole("Employee") || User.IsInRole("Manager")) 
                    {
                        return Forbid();
                    }
                }
                else
                {
                    if (plant.OwnerUserId != user.Id) return Forbid();
                }

                // Free the field if plant is assigned to one
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
        public async Task<IActionResult> Harvest(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.FindAsync(id);
            if (plant == null) return NotFound();

            // Authorization Check
            if (user.CompanyId != null)
            {
                if (plant.CompanyId != user.CompanyId) return Forbid();
                
                if (User.IsInRole("Employee"))
                {
                    return Forbid();
                }
            }
            else
            {
                if (plant.OwnerUserId != user.Id) return Forbid();
            }

            // Mark plant as harvested
            plant.Status = "Harvested";
            plant.UpdatedDate = System.DateTime.UtcNow;

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

            _context.Update(plant);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
