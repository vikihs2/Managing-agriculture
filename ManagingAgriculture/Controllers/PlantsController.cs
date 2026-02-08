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
                    .Where(p => p.CompanyId == user.CompanyId || (p.CompanyId == null && p.OwnerUserId == user.Id))
                    .ToListAsync();
            }
            else
            {
                plants = await _context.Plants.Where(p => p.OwnerUserId == user.Id).ToListAsync();
            }
            
            return View(plants);
        }

        [HttpGet]
        public IActionResult Add()
        {
            ViewData["Title"] = "Add Plant";
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
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            // Use CurrentTrackingDate if provided, otherwise use today
            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;
            
            // Calculate Growth % using the new algorithm
            int growthPercent = _cropService.CalculateGrowthPercentage(
                model.PlantedDate,
                trackingDate,
                model.CropType,
                model.SoilType ?? "",
                model.AvgTemperatureCelsius,
                model.IsIndoor,
                model.WateringFrequencyDays,
                model.SunlightExposure ?? ""
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
                SoilType = model.SoilType,
                SunlightExposure = model.SunlightExposure,
                IsIndoor = model.IsIndoor,
                AvgTemperatureCelsius = model.AvgTemperatureCelsius,
                WateringFrequencyDays = model.WateringFrequencyDays,
                CreatedDate = System.DateTime.UtcNow,
                UpdatedDate = System.DateTime.UtcNow,
                CompanyId = user.CompanyId,
                OwnerUserId = user.CompanyId == null ? user.Id : null
            };

            _context.Plants.Add(plant);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.FindAsync(id);

            if (plant == null) return NotFound();

            if (user.CompanyId != null)
            {
                // Company Logic
                if (plant.CompanyId != user.CompanyId) return Forbid();
                
                // Role Restrictions
                if (User.IsInRole("Employee") || User.IsInRole("Manager")) 
                {
                    // Manager can ADD, but not EDIT or DELETE.
                    // Employee can only VIEW.
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
                NextTask = plant.NextTask,
                Notes = plant.Notes,
                SoilType = plant.SoilType,
                SunlightExposure = plant.SunlightExposure,
                IsIndoor = plant.IsIndoor,
                AvgTemperatureCelsius = plant.AvgTemperatureCelsius,
                WateringFrequencyDays = plant.WateringFrequencyDays
            };
            
            ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
            ViewBag.Id = plant.Id;
            
            // Get crop recommendations
            var (recSoil, recSun, recTemp, recWater) = _cropService.GetCropRecommendations(plant.PlantType);
            ViewBag.RecommendedSoil = recSoil;
            ViewBag.RecommendedSunlight = recSun;
            ViewBag.RecommendedTemperature = recTemp;
            ViewBag.RecommendedWater = recWater;
            
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
                ViewBag.CropCategories = ManagingAgriculture.Services.CropDataService.CropCategories;
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plant = await _context.Plants.FindAsync(id);

            if (plant == null) return NotFound();

             // Authorization
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
            
            // Use CurrentTrackingDate if provided, otherwise use today
            var trackingDate = model.CurrentTrackingDate ?? System.DateTime.Today;
            
            // Calculate growth % using the new algorithm
            int growthPercent = _cropService.CalculateGrowthPercentage(
                model.PlantedDate,
                trackingDate,
                model.CropType,
                model.SoilType ?? "",
                model.AvgTemperatureCelsius,
                model.IsIndoor,
                model.WateringFrequencyDays,
                model.SunlightExposure ?? ""
            );

            plant.Name = model.Name;
            plant.PlantType = model.CropType;
            plant.PlantedDate = model.PlantedDate;
            plant.CurrentTrackingDate = trackingDate;
            plant.GrowthStagePercent = growthPercent;
            plant.NextTask = model.NextTask;
            plant.Notes = model.Notes;
            plant.SoilType = model.SoilType;
            plant.SunlightExposure = model.SunlightExposure;
            plant.IsIndoor = model.IsIndoor;
            plant.AvgTemperatureCelsius = model.AvgTemperatureCelsius;
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

                _context.Plants.Remove(plant);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
    }
}
