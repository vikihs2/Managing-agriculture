using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;

namespace ManagingAgriculture.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // ========== 1. SEED ROLES ==========
            string[] roleNames = { "SystemAdmin", "ITSupport", "Boss", "Manager", "Employee", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }
            Console.WriteLine("[SEED] Roles created.");

            // ========== 2. SEED USERS ==========
            var usersToCreate = new[]
            {
                new { UserName = "admin",   Password = "admin1234",  Role = "SystemAdmin", First = "Admin",   Last = "System"  },
                new { UserName = "it",      Password = "It1234",     Role = "ITSupport",   First = "IT",      Last = "Support" },
                new { UserName = "bossBob", Password = "100% BoB",   Role = "Boss",        First = "Bob",     Last = "Boss"    },
                new { UserName = "viki1",   Password = "100% FuN",   Role = "Manager",     First = "Viki",    Last = "Manager" },
                new { UserName = "ivan",    Password = "100% IvaN",  Role = "Employee",    First = "Ivan",    Last = "Ivanov"  },
                new { UserName = "gosho",   Password = "100% GoG",   Role = "User",        First = "Gosho",   Last = "Georgiev"}
            };

            var seededUsers = new List<ApplicationUser>();

            foreach (var u in usersToCreate)
            {
                var email = u.UserName.ToLower() == "gosho" ? "georgi@gmail.com" : $"{u.UserName.ToLower()}@gmail.com";
                var user = await userManager.FindByNameAsync(u.UserName);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName   = u.UserName,
                        Email      = email,
                        EmailConfirmed = true,
                        FirstName  = u.First,
                        LastName   = u.Last
                    };
                    var result = await userManager.CreateAsync(user, u.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, u.Role);
                        Console.WriteLine($"[SEED] User '{u.UserName}' created with role '{u.Role}'.");
                    }
                    else
                    {
                        Console.WriteLine($"[SEED ERROR] Failed to create '{u.UserName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine($"[SEED] User '{u.UserName}' already exists. Forcing email and password reset...");
                    
                    // Force update email
                    user.Email = email;
                    user.NormalizedEmail = email.ToUpper();
                    await userManager.UpdateAsync(user);

                    // Force reset password
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await userManager.ResetPasswordAsync(user, token, u.Password);
                    if (!resetResult.Succeeded)
                    {
                        Console.WriteLine($"[SEED ERROR] Failed to reset password for {u.UserName}: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
                    }

                    if (!await userManager.IsInRoleAsync(user, u.Role))
                        await userManager.AddToRoleAsync(user, u.Role);
                }

                seededUsers.Add(user);
            }

            // ========== 2.5 ENSURE COMPANY FOR BOSS ==========
            var bossUser = await userManager.FindByNameAsync("bossBob");
            var managerUser = await userManager.FindByNameAsync("viki1");
            var employeeUser = await userManager.FindByNameAsync("ivan");

            if (bossUser != null)
            {
                var company = await context.Companies.FirstOrDefaultAsync(c => c.CreatedByUserId == bossUser.Id);
                if (company == null)
                {
                    company = new Company
                    {
                        Name = "Bob's Farming Empire",
                        CreatedByUserId = bossUser.Id,
                        CreatedOn = DateTime.UtcNow
                    };
                    context.Companies.Add(company);
                    await context.SaveChangesAsync();
                }

                // Update users with CompanyId
                var usersToUpdate = new[] { bossUser, managerUser, employeeUser };
                foreach (var uToUpdate in usersToUpdate)
                {
                    if (uToUpdate != null && uToUpdate.CompanyId != company.Id)
                    {
                        uToUpdate.CompanyId = company.Id;
                        await userManager.UpdateAsync(uToUpdate);
                    }
                }
                
                // Refresh seededUsers list to have the latest CompanyId
                for(int i = 0; i < seededUsers.Count; i++) {
                    seededUsers[i] = await userManager.FindByIdAsync(seededUsers[i].Id);
                }
            }

            // ========== 3. CHECK IF DATA ALREADY SEEDED ==========
            // Require exactly 121 plants (6 users × 20 + 1 for force reseed). If less, wipe and reseed.
            var existingPlantCount = await context.Plants.CountAsync();
            if (existingPlantCount >= 121)
            {
                Console.WriteLine($"[SEED] Data already fully seeded ({existingPlantCount} plants). Skipping.");
                return;
            }
            if (existingPlantCount > 0)
            {
                Console.WriteLine($"[SEED] Found {existingPlantCount} plants (forcing reseed to apply company logic). Clearing and reseeding...");
            }
            
            // ========== 4. CLEAR STALE DATA (FK-safe order) ==========
            Console.WriteLine("[SEED] Clearing old data...");
            try
            {
                context.SensorReadings.RemoveRange(context.SensorReadings);
                context.Sensors.RemoveRange(context.Sensors);
                context.MarketplacePurchaseRequests.RemoveRange(context.MarketplacePurchaseRequests);
                context.ResourceUsages.RemoveRange(context.ResourceUsages);
                context.MaintenanceHistory.RemoveRange(context.MaintenanceHistory);
                context.TaskAssignments.RemoveRange(context.TaskAssignments);
                context.LeaveRequests.RemoveRange(context.LeaveRequests);
                context.LeaveRecords.RemoveRange(context.LeaveRecords);
                context.HarvestRecords.RemoveRange(context.HarvestRecords);
                context.ContactForms.RemoveRange(context.ContactForms);
                context.MarketplaceListings.RemoveRange(context.MarketplaceListings);

                var fieldsWithPlants = await context.Fields.Where(f => f.CurrentPlantId != null).ToListAsync();
                foreach (var f in fieldsWithPlants) { f.CurrentPlantId = null; f.IsOccupied = false; }
                await context.SaveChangesAsync();

                context.Plants.RemoveRange(context.Plants);
                context.Fields.RemoveRange(context.Fields);
                context.Machinery.RemoveRange(context.Machinery);
                context.Resources.RemoveRange(context.Resources);
                await context.SaveChangesAsync();
                
                // Cleanup old rogue users that cause duplicate email issues (like 'it_support')
                var validUsernames = new[] { "admin", "it", "bossBob", "viki1", "ivan", "gosho" };
                var rogueUsers = await context.Users.Where(u => !validUsernames.Contains(u.UserName)).ToListAsync();
                foreach (var rUser in rogueUsers)
                {
                    await userManager.DeleteAsync(rUser);
                }

                Console.WriteLine("[SEED] Old data cleared.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED ERROR] Clear failed: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[SEED INNER] {ex.InnerException.Message}");
                return;
            }

            // ========== 5. MASTER DATA ARRAYS ==========

            // 5 unique field sets per user (Bulgarian cities/locations)
            string[][] fieldNameSets =
            {
                new[] { "North Wheat Field",    "South Corn Plot",       "East Vineyard",         "West Sunflower Field",  "Central Apple Orchard"  },
                new[] { "Hilltop Pasture",      "River Valley Field",    "Mountain Terrace Plot",  "Lakeshore Meadow",     "Forest Edge Field"       },
                new[] { "Golden Plains Field",  "Silver Creek Plot",     "Bronze Hill Terrace",    "Emerald Meadow",       "Ruby Ridge Field"        },
                new[] { "Morning Dew Meadow",   "Sunset Valley Field",   "Midnight Pasture",       "Dawn Ridge Plot",      "Twilight Lowland"        },
                new[] { "Alpha Field A",        "Beta Terrace B",        "Gamma Valley C",         "Delta Plateau D",      "Epsilon Lowland E"       },
                new[] { "Rose Garden Field",    "Lavender Plateau",      "Tulip Valley Plot",      "Daisy Meadow East",    "Lily Pond Field"         }
            };
            string[] fieldCities      = { "Sofia", "Plovdiv", "Varna", "Burgas", "Ruse" };
            string[] soilTypes        = { "Loamy", "Clay", "Sandy", "Peaty", "Chalky" };
            string[] sunlightOptions  = { "Full Sun", "Partial Shade", "Full Sun", "Partial Shade", "Full Sun" };
            decimal[] fieldTemps      = { 18m, 20m, 16m, 22m, 19m };
            decimal[] fieldSizes      = { 25m, 40m, 15m, 60m, 30m };

            // 20 unique plants
            string[] plantNames = {
                "Cherry Tomato",       "Winter Wheat",        "Sweet Corn",           "Giant Sunflower",
                "Yukon Gold Potato",   "Nantes Carrot",       "Red Onion",            "Hardneck Garlic",
                "Savoy Cabbage",       "Bell Pepper",         "Black-Eyed Peas",      "Butternut Squash",
                "Sugar Beet",          "Alfalfa",             "Lavender",             "Green Peas",
                "Watermelon",          "Rye",                 "Soybean",              "Oat Grass"
            };
            string[] plantTypes = {
                "Tomato",   "Wheat",    "Corn",       "Sunflower",
                "Potato",   "Carrot",   "Onion",      "Garlic",
                "Cabbage",  "Pepper",   "Legume",     "Squash",
                "Beet",     "Alfalfa",  "Lavender",   "Peas",
                "Melon",    "Rye",      "Soybean",    "Oat"
            };
            string[] nextTasks        = { "Water", "Fertilize", "Inspect", "Prune", "Harvest", "Re-pot", "Spray", "Weed" };
            string[] plantLocations   = {
                "Field A", "Greenhouse 1", "Field B", "Greenhouse 2", "Field C",
                "Field D", "Open Air",     "Field E", "Polytunnel",   "Field F",
                "Field G", "Greenhouse 3", "Field H", "Field I",      "Open Air",
                "Field J", "Field K",      "Greenhouse 4","Field L",  "Field M"
            };

            // 10 unique machinery items
            string[] machineryNames = {
                "John Deere 5075E Tractor",   "Massey Ferguson MF 5711",   "New Holland T6.180 Harvester",
                "Case IH Puma 150 Combine",   "Kubota M6-142 Tractor",     "CLAAS Arion 660 Baler",
                "Fendt 724 Vario Sprayer",    "Deutz-Fahr 6175 TTV Plow",  "Valtra T235 Direct Seeder",
                "JCB Fastrac 4220 Loader"
            };
            string[] machineryTypes    = { "Tractor", "Tractor", "Harvester", "Combine", "Tractor", "Baler",   "Sprayer",  "Plow",    "Seeder",   "Loader"    };
            string[] machineryStatuses = { "Excellent","Good",    "Good",      "Fair",    "Excellent","Good",    "Excellent","Good",    "Fair",     "Excellent" };
            decimal[] machineryPrices  = { 55000m,     48000m,    95000m,      112000m,   42000m,     28000m,    33000m,    19000m,    25000m,    67000m      };
            decimal[] machineryHours   = { 1240m,      870m,      3100m,       4500m,     560m,       2200m,     980m,      1750m,     430m,      610m        };

            // 10 unique resources
            string[] resourceNames      = {
                "NPK Fertilizer 20-20-20",  "Phosphorus Super Blend",   "Potassium Chloride 60%",
                "Ammonium Nitrate 34%",     "Organic Compost Premium",  "Roundup Pro Herbicide",
                "Chlorpyrifos Pesticide",   "Mancozeb Fungicide 80%",   "Diesel Fuel B7",
                "Drip Irrigation Water"
            };
            string[] resourceCategories = { "Fertilizer","Fertilizer","Fertilizer","Fertilizer","Soil",     "Chemical", "Chemical", "Chemical", "Fuel",     "Water"  };
            string[] resourceUnits      = { "Kg",        "Kg",        "Kg",        "Kg",        "Kg",       "Liters",   "Liters",   "Kg",       "Liters",   "Liters" };
            decimal[] resourceQty       = { 500m,        300m,        200m,        450m,        800m,       120m,       95m,        180m,       1200m,      5000m    };
            decimal[] resourceThreshold = { 50m,         30m,         25m,         40m,         80m,        15m,        10m,        20m,        100m,       500m     };
            string[]  resourceSuppliers = { "AgroMax Ltd.", "BulFert Co.", "SeedMaster BG", "EuroAgri Supply", "Green Valley Organics", "ChemFarm Inc.",
                                            "AgroMax Ltd.", "BulFert Co.", "PetrolAgro BG", "IrrigaSys Ltd." };

            // 10 marketplace listings
            string[] listingItemNames = {
                "John Deere 6120M Tractor",         "Premium Wheat Seeds (50kg bag)",   "Hydraulic Post Hole Digger",
                "Organic Cherry Tomatoes (500 kg)", "Kubota Mini Excavator KX040-4",    "Cold-Pressed Sunflower Oil (200L)",
                "CLAAS Lexion Combine Harvester",   "Fresh Potato Harvest (2 Tonnes)",  "3-Phase Irrigation Pump System",
                "Corn Silage Round Bales (x50)"
            };
            string[] listingCategories  = { "Tractors", "Seeds",   "Equipment", "Produce",  "Equipment",  "Produce",   "Harvesters", "Produce",  "Equipment",  "Produce"      };
            string[] listingConditions  = { "Excellent","Like New","Good",      "Fresh",    "Good",       "Fresh",     "Fair",       "Fresh",    "Like New",   "Fresh"        };
            string[] listingTypes       = { "Sale",     "Sale",    "Sale or Rent","Sale",   "Rent",       "Sale",      "Sale",       "Sale",     "Sale or Rent","Sale"        };
            decimal?[] salePrices       = { 48500m,     280m,      null,         1200m,     null,         650m,        72000m,       1800m,      null,          4200m         };
            decimal?[] rentalPrices     = { null,       null,      180m,         null,      220m,         null,        null,         null,       95m,           null          };

            var random = new Random(42);

            // ========== 6. SEED DATA FOR EACH USER ==========
            for (int userIdx = 0; userIdx < seededUsers.Count; userIdx++)
            {
                var user   = seededUsers[userIdx];
                var userId = user.Id;
                var uName  = user.UserName ?? "user";
                var compId = user.CompanyId;
                Console.WriteLine($"[SEED] Seeding data for '{uName}'...");

                // ----- 5 Fields -----
                for (int i = 0; i < 5; i++)
                {
                    context.Fields.Add(new Field
                    {
                        Name                     = fieldNameSets[userIdx % fieldNameSets.Length][i],
                        CompanyId                = compId,
                        SizeInDecars             = fieldSizes[i] + (userIdx * 3m),
                        City                     = fieldCities[i],
                        SoilType                 = soilTypes[i],
                        SunlightExposure         = sunlightOptions[i],
                        AverageTemperatureCelsius= fieldTemps[i],
                        IsOccupied               = false,
                        OwnerUserId              = userId,
                        CreatedDate              = DateTime.UtcNow.AddDays(-random.Next(30, 365)),
                        UpdatedDate              = DateTime.UtcNow
                    });
                }

                // ----- 10 Resources -----
                for (int i = 0; i < 10; i++)
                {
                    context.Resources.Add(new Resource
                    {
                        Name              = resourceNames[i],
                        CompanyId         = compId,
                        Category          = resourceCategories[i],
                        Quantity          = resourceQty[i] + (userIdx * 10m),
                        Unit              = resourceUnits[i],
                        LowStockThreshold = resourceThreshold[i],
                        Supplier          = resourceSuppliers[i],
                        OwnerUserId       = userId,
                        CreatedDate       = DateTime.UtcNow.AddDays(-random.Next(10, 200)),
                        UpdatedDate       = DateTime.UtcNow
                    });
                }

                // ----- 10 Machinery -----
                for (int i = 0; i < 10; i++)
                {
                    context.Machinery.Add(new Machinery
                    {
                        Name            = machineryNames[i],
                        CompanyId       = compId,
                        Type            = machineryTypes[i],
                        PurchaseDate    = DateTime.UtcNow.AddYears(-random.Next(1, 8)).AddDays(-random.Next(0, 365)),
                        Status          = machineryStatuses[i],
                        PurchasePrice   = machineryPrices[i],
                        EngineHours     = machineryHours[i] + (userIdx * 50m),
                        LastServiceDate = DateTime.UtcNow.AddMonths(-random.Next(1, 8)),
                        NextServiceDate = DateTime.UtcNow.AddMonths(random.Next(1, 6)),
                        OwnerUserId     = userId,
                        CreatedDate     = DateTime.UtcNow.AddDays(-random.Next(30, 500)),
                        UpdatedDate     = DateTime.UtcNow
                    });
                }

                // ----- 20 Unique Plants -----
                for (int i = 0; i < 20; i++)
                {
                    int growthPct = 10 + (i * 4) + random.Next(0, 8); // 10 % → ~86 % spread
                    if (growthPct > 99) growthPct = 99;

                    context.Plants.Add(new Plant
                    {
                        Name                 = $"{uName}'s {plantNames[i]}",
                        CompanyId            = compId,
                        PlantType            = plantTypes[i],
                        PlantedDate          = DateTime.UtcNow.AddDays(-random.Next(20, 180)),
                        GrowthStagePercent   = growthPct,
                        NextTask             = nextTasks[i % nextTasks.Length],
                        Status               = growthPct >= 90 ? "Ready to Harvest" : "Active",
                        Location             = plantLocations[i],
                        Notes                = $"{plantNames[i]} growing well. Regular monitoring recommended.",
                        IsIndoor             = i % 6 == 0,   // every 6th is indoor
                        WateringFrequencyDays= 2 + (i % 5),
                        OwnerUserId          = userId,
                        CreatedDate          = DateTime.UtcNow.AddDays(-random.Next(20, 180)),
                        UpdatedDate          = DateTime.UtcNow
                    });
                }

                // ----- 10 Marketplace Listings -----
                for (int i = 0; i < 10; i++)
                {
                    string phone = $"+3598{80000000 + (userIdx * 1000000) + (i * 100000 + random.Next(0, 99999))}";
                    context.MarketplaceListings.Add(new MarketplaceListing
                    {
                        ItemName          = listingItemNames[i],
                        Category          = listingCategories[i],
                        ConditionStatus   = listingConditions[i],
                        Description       = $"Quality {listingItemNames[i]} offered by {uName}. Well maintained and ready for immediate use. Contact for more details.",
                        SalePrice         = salePrices[i],
                        RentalPricePerDay = rentalPrices[i],
                        SellerPhone     = phone,
                        ListingType     = listingTypes[i],
                        ListingStatus   = "Active",
                        EngineHours     = (listingCategories[i] is "Tractors" or "Equipment" or "Harvesters")
                                            ? 200m + (i * 150m) + random.Next(0, 200) : null,
                        SellerUserId    = userId,
                        CreatedDate     = DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                        UpdatedDate     = DateTime.UtcNow
                    });
                }
            }

            // ========== 7. SAVE ALL ==========
            try
            {
                await context.SaveChangesAsync();
                Console.WriteLine($"[SEED] SUCCESS! Seeded {seededUsers.Count} users.");
                Console.WriteLine($"[SEED]   Fields:           {seededUsers.Count * 5}");
                Console.WriteLine($"[SEED]   Resources:        {seededUsers.Count * 10}");
                Console.WriteLine($"[SEED]   Machinery:        {seededUsers.Count * 10}");
                Console.WriteLine($"[SEED]   Plants:           {seededUsers.Count * 20}");
                Console.WriteLine($"[SEED]   Market Listings:  {seededUsers.Count * 10}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED ERROR] Save failed: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[SEED INNER] {ex.InnerException.Message}");
            }
        }
    }
}
