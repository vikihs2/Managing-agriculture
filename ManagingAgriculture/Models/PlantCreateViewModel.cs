using System;
using System.ComponentModel.DataAnnotations;

namespace ManagingAgriculture.Models
{
    public class PlantCreateViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Crop Type")]
        public string CropType { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Planted Date")]
        public DateTime PlantedDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Current Date")]
        public DateTime? CurrentTrackingDate { get; set; }

        [Range(0, 100)]
        [Display(Name = "Growth Stage (%)")]
        public int GrowthStage { get; set; }

        [StringLength(200)]
        [Display(Name = "Next Task")]
        public string? NextTask { get; set; }

        public string? Notes { get; set; }

        [StringLength(50)]
        [Display(Name = "Soil Type")]
        public string? SoilType { get; set; }

        [StringLength(50)]
        [Display(Name = "Sunlight Exposure")]
        public string? SunlightExposure { get; set; }

        [Display(Name = "Indoor Plant")]
        public bool IsIndoor { get; set; }

        [Range(-50, 60)]
        [Display(Name = "Average Temperature (°C)")]
        public decimal? AvgTemperatureCelsius { get; set; }

        [Range(0, 365)]
        [Display(Name = "Watering Frequency (days)")]
        public int? WateringFrequencyDays { get; set; }
    }
}
