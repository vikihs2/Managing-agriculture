using System;
using System.Collections.Generic;
using System.Linq;

namespace ManagingAgriculture.Services
{
    public class CropDataService
    {
        public static readonly Dictionary<string, List<string>> CropCategories = new Dictionary<string, List<string>>
        {
            { "🌾 Grains & Cereals", new List<string> { "Wheat", "Corn (Maize)", "Rice", "Barley", "Oats", "Rye", "Sorghum", "Millet" } },
            { "🥔 Root & Tuber Crops", new List<string> { "Potato", "Sweet Potato", "Carrot", "Beetroot", "Turnip", "Radish", "Cassava" } },
            { "🍅 Vegetables (Very Common)", new List<string> { "Tomato", "Cucumber", "Bell Pepper", "Chili Pepper", "Eggplant", "Onion", "Garlic", "Lettuce", "Spinach", "Cabbage", "Broccoli", "Cauliflower", "Zucchini" } },
            { "🌱 Legumes", new List<string> { "Beans (Green Beans)", "Peas", "Lentils", "Chickpeas", "Soybean" } },
            { "🍓 Fruits (Field / Greenhouse)", new List<string> { "Strawberry", "Watermelon", "Melon", "Pumpkin", "Squash" } },
            { "🌿 Industrial / Oil Crops", new List<string> { "Sunflower", "Rapeseed (Canola)", "Cotton", "Sugar Beet", "Sugarcane" } },
            { "🌿 Herbs (Common & Useful)", new List<string> { "Basil", "Parsley", "Dill", "Mint", "Oregano", "Thyme" } },
            { "🌲 Perennial / Special", new List<string> { "Alfalfa", "Clover", "Tobacco" } }
        };

        // Dictionary mapping crop names to their typical days to maturity
        public static readonly Dictionary<string, int> CropMaturityDays = new Dictionary<string, int>
        {
            // Grains & Cereals (100-200 days typical)
            { "Wheat", 150 },
            { "Corn (Maize)", 120 },
            { "Rice", 150 },
            { "Barley", 140 },
            { "Oats", 120 },
            { "Rye", 140 },
            { "Sorghum", 130 },
            { "Millet", 90 },
            
            // Root & Tuber Crops (70-150 days)
            { "Potato", 90 },
            { "Sweet Potato", 120 },
            { "Carrot", 80 },
            { "Beetroot", 70 },
            { "Turnip", 75 },
            { "Radish", 30 },
            { "Cassava", 365 },
            
            // Vegetables (50-100 days mostly)
            { "Tomato", 75 },
            { "Cucumber", 60 },
            { "Bell Pepper", 90 },
            { "Chili Pepper", 90 },
            { "Eggplant", 80 },
            { "Onion", 120 },
            { "Garlic", 240 },
            { "Lettuce", 50 },
            { "Spinach", 45 },
            { "Cabbage", 90 },
            { "Broccoli", 80 },
            { "Cauliflower", 85 },
            { "Zucchini", 50 },
            
            // Legumes (60-100 days)
            { "Beans (Green Beans)", 60 },
            { "Peas", 65 },
            { "Lentils", 110 },
            { "Chickpeas", 120 },
            { "Soybean", 140 },
            
            // Fruits (60-150 days)
            { "Strawberry", 180 },
            { "Watermelon", 90 },
            { "Melon", 85 },
            { "Pumpkin", 120 },
            { "Squash", 100 },
            
            // Industrial / Oil Crops (90-180 days)
            { "Sunflower", 100 },
            { "Rapeseed (Canola)", 140 },
            { "Cotton", 160 },
            { "Sugar Beet", 150 },
            { "Sugarcane", 365 },
            
            // Herbs (30-90 days)
            { "Basil", 50 },
            { "Parsley", 70 },
            { "Dill", 45 },
            { "Mint", 90 },
            { "Oregano", 90 },
            { "Thyme", 90 },
            
            // Perennial / Special
            { "Alfalfa", 90 },
            { "Clover", 90 },
            { "Tobacco", 120 }
        };

        public static List<string> GetAllCrops()
        {
            return CropCategories.Values.SelectMany(x => x).OrderBy(x => x).ToList();
        }

        /// <summary>
        /// Get the typical days to maturity for a crop type
        /// </summary>
        public int GetCropMaturityDays(string cropType)
        {
            if (string.IsNullOrEmpty(cropType)) return 100; // Default fallback
            
            if (CropMaturityDays.TryGetValue(cropType, out var days))
                return days;
            
            return 100; // Default to 100 days if crop not found
        }

        /// <summary>
        /// Calculate growth percentage based on time + environmental factors
        /// Returns: 0% if not planted, growth % if healthy, negative if plant is dead
        /// Formula: (days_elapsed / maturity_days) * 100 * environmental_multiplier
        /// Environmental multiplier: Based on soil, water, light, temp, indoor/outdoor conditions
        /// Plant dies if conditions are critically bad for extended period
        /// </summary>
        public int CalculateGrowthPercentage(
            DateTime plantedDate,
            DateTime currentDate,
            string cropType,
            string soilType,
            decimal? avgTemperature,
            bool isIndoor,
            int? wateringFrequencyDays,
            string sunlightExposure)
        {
            // If current date is before planted date, plant not yet planted
            if (currentDate < plantedDate)
                return 0;
            
            // Get crop maturity days
            int maturityDays = GetCropMaturityDays(cropType);
            
            // Calculate days elapsed since planting
            TimeSpan elapsed = currentDate - plantedDate;
            double elapsedDays = elapsed.TotalDays;
            
            // Time-based progress (days_elapsed / maturity_days * 100)
            double timeProgress = (elapsedDays / maturityDays) * 100.0;
            
            // Calculate environmental conditions multiplier (0.0 to 2.0)
            double envMultiplier = CalculateEnvironmentalMultiplier(
                cropType,
                soilType,
                avgTemperature,
                isIndoor,
                wateringFrequencyDays,
                sunlightExposure
            );
            
            // If conditions critically bad for more than 10 days, plant dies
            if (envMultiplier < 0.2 && elapsedDays > 10)
                return -1; // Plant is DEAD
            
            // Calculate final growth: time-based (70%) * environmental multiplier (30%)
            double growthPercent = (timeProgress * 0.7) + (timeProgress * envMultiplier * 0.3);
            
            // Cap at 100% (at 100%, plant is ready for harvest)
            if (growthPercent > 100) growthPercent = 100;
            if (growthPercent < 0) growthPercent = 0;
            
            return (int)growthPercent;
        }

        /// <summary>
        /// Calculate environmental conditions multiplier (0.0 to 2.0)
        /// 1.0 = optimal conditions
        /// &lt; 0.2 = critically bad (plant dies if prolonged)
        /// Returns lower multiplier if conditions are worse
        /// </summary>
        private double CalculateEnvironmentalMultiplier(
            string cropType,
            string soilType,
            decimal? avgTemperature,
            bool isIndoor,
            int? wateringFrequencyDays,
            string sunlightExposure)
        {
            double multiplier = 1.0;
            
            // 1. WATER ANALYSIS (-40% to +10% based on watering frequency)
            if (wateringFrequencyDays.HasValue)
            {
                int waterDays = wateringFrequencyDays.Value;
                
                // Ideal watering frequency for most crops: 2-4 days
                if (waterDays >= 2 && waterDays <= 4)
                    multiplier *= 1.0; // Optimal
                else if (waterDays == 1 || (waterDays >= 5 && waterDays <= 6))
                    multiplier *= 0.8; // Suboptimal (-20%)
                else if (waterDays == 0)
                    multiplier *= 0.1; // Critical: over-watering causes root rot (-90%)
                else if (waterDays >= 7)
                    multiplier *= 0.3; // Critical: under-watering causes wilting (-70%)
            }
            
            // 2. TEMPERATURE ANALYSIS (-50% to +15%)
            if (avgTemperature.HasValue)
            {
                decimal temp = avgTemperature.Value;
                
                // Most crops prefer 15-30°C
                if (temp >= 15 && temp <= 30)
                    multiplier *= 1.0; // Optimal
                else if ((temp >= 10 && temp < 15) || (temp > 30 && temp <= 35))
                    multiplier *= 0.8; // Suboptimal (-20%)
                else if ((temp >= 5 && temp < 10) || (temp > 35 && temp <= 40))
                    multiplier *= 0.4; // Bad (-60%)
                else
                    multiplier *= 0.1; // Critical: plant dying (-90%)
            }
            
            // 3. SUNLIGHT ANALYSIS (-50% to +20%)
            if (!string.IsNullOrEmpty(sunlightExposure) && !string.IsNullOrEmpty(cropType))
            {
                var lowerCrop = cropType.ToLower();
                var lowerSun = sunlightExposure.ToLower();
                
                // Full sun crops (tomato, pepper, corn, sunflower, etc.)
                if (lowerCrop.Contains("tomato") || lowerCrop.Contains("pepper") || 
                    lowerCrop.Contains("corn") || lowerCrop.Contains("sunflower") ||
                    lowerCrop.Contains("melon") || lowerCrop.Contains("watermelon"))
                {
                    if (lowerSun.Contains("full sun"))
                        multiplier *= 1.1; // Optimal (+10%)
                    else if (lowerSun.Contains("partial sun"))
                        multiplier *= 0.8; // Suboptimal (-20%)
                    else if (lowerSun.Contains("partial shade"))
                        multiplier *= 0.4; // Bad (-60%)
                    else if (lowerSun.Contains("full shade"))
                        multiplier *= 0.1; // Critical: plant dying (-90%)
                }
                // Leafy greens (can tolerate shade)
                else if (lowerCrop.Contains("lettuce") || lowerCrop.Contains("spinach") || 
                         lowerCrop.Contains("cabbage") || lowerCrop.Contains("parsley"))
                {
                    if (lowerSun.Contains("partial shade") || lowerSun.Contains("partial sun"))
                        multiplier *= 1.0; // Optimal
                    else if (lowerSun.Contains("full sun"))
                        multiplier *= 0.9; // Slightly suboptimal (-10%)
                    else if (lowerSun.Contains("full shade"))
                        multiplier *= 0.5; // Bad (-50%)
                }
                // Root crops (less light sensitive)
                else
                {
                    if (lowerSun.Contains("full shade"))
                        multiplier *= 0.7; // Suboptimal (-30%)
                    else if (lowerSun.Contains("full sun"))
                        multiplier *= 1.0; // Optimal
                }
            }
            
            // 4. SOIL TYPE ANALYSIS (-30% to +10%)
            if (!string.IsNullOrEmpty(soilType) && !string.IsNullOrEmpty(cropType))
            {
                var lowerCrop = cropType.ToLower();
                var lowerSoil = soilType.ToLower();
                
                // Root/Tuber crops prefer sandy or loamy
                if (lowerCrop.Contains("carrot") || lowerCrop.Contains("potato") || 
                    lowerCrop.Contains("radish") || lowerCrop.Contains("turnip") || 
                    lowerCrop.Contains("beet") || lowerCrop.Contains("cassava"))
                {
                    if (lowerSoil.Contains("loamy") || lowerSoil.Contains("sandy"))
                        multiplier *= 1.0; // Optimal
                    else if (lowerSoil.Contains("silty"))
                        multiplier *= 0.9; // Slightly suboptimal (-10%)
                    else if (lowerSoil.Contains("clay"))
                        multiplier *= 0.5; // Bad (-50%)
                }
                // Vegetables prefer loamy
                else if (lowerCrop.Contains("tomato") || lowerCrop.Contains("pepper") || 
                         lowerCrop.Contains("cucumber") || lowerCrop.Contains("lettuce"))
                {
                    if (lowerSoil.Contains("loamy"))
                        multiplier *= 1.0; // Optimal
                    else if (lowerSoil.Contains("sandy") || lowerSoil.Contains("clay"))
                        multiplier *= 0.8; // Suboptimal (-20%)
                }
            }
            
            // 5. INDOOR/OUTDOOR CONSIDERATION
            // Plants in greenhouses/indoors can compensate some factors
            if (isIndoor)
                multiplier *= 1.05; // +5% bonus for controlled environment
            
            // Cap multiplier at reasonable values
            if (multiplier > 2.0) multiplier = 2.0;
            if (multiplier < 0) multiplier = 0;
            
            return multiplier;
        }

        /// <summary>
        /// Get plant status: Healthy, Stressed, Dying, or Dead
        /// </summary>
        public string GetPlantStatus(int growthPercent, DateTime plantedDate, DateTime currentDate)
        {
            if (growthPercent < 0)
                return "Dead ☠️";
            else if (growthPercent < 30)
                return "Dying 🌱";
            else if (growthPercent < 70)
                return "Stressed ⚠️";
            else
                return "Healthy 🌿";
        }

        /// <summary>
        /// Get recommended conditions for a crop type
        /// Returns tuple: (soilType, sunlight, tempMin, tempMax, waterDaysMin, waterDaysMax)
        /// </summary>
        public (string soil, string sunlight, string temperature, string water) GetCropRecommendations(string cropType)
        {
            var lowerCrop = cropType?.ToLower() ?? "";
            
            // Full sun crops (tomato, pepper, corn, sunflower)
            if (lowerCrop.Contains("tomato") || lowerCrop.Contains("pepper") || 
                lowerCrop.Contains("corn") || lowerCrop.Contains("sunflower") || lowerCrop.Contains("melon"))
                return ("Loamy soil", "Full Sun (6-8h)", "18-28°C", "Water every 2-3 days");
            
            // Root crops (potato, carrot)
            else if (lowerCrop.Contains("potato") || lowerCrop.Contains("carrot") || lowerCrop.Contains("turnip") || lowerCrop.Contains("radish"))
                return ("Sandy/Loamy soil", "Partial Sun (4-6h)", "15-25°C", "Water every 3-4 days");
            
            // Leafy greens (lettuce, spinach, cabbage)
            else if (lowerCrop.Contains("lettuce") || lowerCrop.Contains("spinach") || lowerCrop.Contains("cabbage") || lowerCrop.Contains("parsley"))
                return ("Loamy soil rich in organic matter", "Partial Shade (3-4h)", "15-22°C", "Water every 2-3 days, keep moist");
            
            // Legumes (beans, peas)
            else if (lowerCrop.Contains("bean") || lowerCrop.Contains("pea") || lowerCrop.Contains("lentil"))
                return ("Well-drained loamy soil", "Full Sun (6-8h)", "16-25°C", "Water every 3-4 days");
            
            // Grains (wheat, rice, barley)
            else if (lowerCrop.Contains("wheat") || lowerCrop.Contains("rice") || lowerCrop.Contains("barley") || lowerCrop.Contains("corn"))
                return ("Well-drained soil", "Full Sun (6-8h)", "15-28°C", "Water every 4-7 days (after germination)");
            
            // Herbs (basil, mint, oregano)
            else if (lowerCrop.Contains("basil") || lowerCrop.Contains("mint") || lowerCrop.Contains("oregano") || lowerCrop.Contains("thyme"))
                return ("Well-drained soil", "Full Sun/Partial Sun (4-6h)", "18-25°C", "Water when soil feels dry (every 2-3 days)");
            
            // Default recommendations
            else
                return ("Loamy soil", "Partial Sun (4-6h)", "15-25°C", "Water every 3 days");
        }

        /// <summary>
        /// Calculate time-based progress percentage
        /// </summary>
        private int CalculateTimeProgress(DateTime plantedDate, DateTime currentDate, int maturityDays)
        {
            if (plantedDate > currentDate)
                return 0; // Not planted yet
            
            TimeSpan elapsed = currentDate - plantedDate;
            double elapsedDays = elapsed.TotalDays;
            
            if (maturityDays <= 0)
                return 0;
            
            double percent = (elapsedDays / maturityDays) * 100.0;
            
            if (percent < 0) return 0;
            if (percent > 100) return 100;
            
            return (int)percent;
        }
    }
}

