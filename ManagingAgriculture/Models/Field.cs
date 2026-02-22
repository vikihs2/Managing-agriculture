using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
    /// <summary>
    /// Represents a physical field in the agricultural operation.
    /// Each field has environmental properties and can have at most one active plant.
    /// </summary>
    public class Field
    {
        /// <summary>Primary key - unique identifier</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>Company ownership</summary>
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>User ownership (if freelancer)</summary>
        public string? OwnerUserId { get; set; }

        /// <summary>Field name/identifier (e.g., 'North Sector 1')</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Size of field in decars</summary>
        [Required]
        [Range(0.1, 10000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal SizeInDecars { get; set; }

        /// <summary>City in Bulgaria where the field is located</summary>
        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>Soil type: Clay, Loamy, Sandy, etc.</summary>
        [Required]
        [StringLength(50)]
        public string SoilType { get; set; } = string.Empty;

        /// <summary>Sunlight exposure: Full Sun, Partial Shade, Shade, etc.</summary>
        [Required]
        [StringLength(50)]
        public string SunlightExposure { get; set; } = string.Empty;

        /// <summary>Average temperature in Celsius</summary>
        [Required]
        [Range(-50, 60)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal AverageTemperatureCelsius { get; set; }

        /// <summary>Whether field is currently occupied by a plant</summary>
        public bool IsOccupied { get; set; } = false;

        /// <summary>Current plant growing on this field (if occupied)</summary>
        public int? CurrentPlantId { get; set; }

        [ForeignKey("CurrentPlantId")]
        public Plant? CurrentPlant { get; set; }

        /// <summary>Record creation date</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Last update date</summary>
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

        // ===== NAVIGATION PROPERTIES =====

        /// <summary>Historical planting records on this field</summary>
        public ICollection<Plant>? Plants { get; set; }
    }
}
