using ManagingAgriculture.Models;

namespace ManagingAgriculture.ViewModels
{
    public class DashboardViewModel
    {
        public int ActivePlantsCount { get; set; }
        public int ResourcesCount { get; set; }
        public int MachineryCount { get; set; }
        public int LowStockCount { get; set; }
        public List<Plant> ActiveCrops { get; set; } = new List<Plant>();
        public List<HarvestRecord> RecentHarvests { get; set; } = new List<HarvestRecord>();
    }
}
