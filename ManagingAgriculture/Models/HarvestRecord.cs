using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
    /// <summary>
    /// Records a harvest event - when a plant reaches 100% and is harvested.
    /// This keeps statistics on what was produced.
    /// </summary>
    public class HarvestRecord
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Company that produced the harvest (null for freelancers)</summary>
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>User who owns this record (freelancer)</summary>
        public string? OwnerUserId { get; set; }

        /// <summary>Name of the plant/crop that was harvested</summary>
        [Required]
        [StringLength(100)]
        public string PlantName { get; set; } = string.Empty;

        /// <summary>Type of crop (Wheat, Potato, etc.)</summary>
        [Required]
        [StringLength(50)]
        public string PlantType { get; set; } = string.Empty;

        /// <summary>Field name where the plant was grown</summary>
        [StringLength(100)]
        public string? FieldName { get; set; }

        /// <summary>Field size in decars (used for yield calculation)</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? FieldSizeDecars { get; set; }

        /// <summary>Estimated yield in kg based on crop type and field size</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedYieldKg { get; set; }

        /// <summary>Date the harvest took place</summary>
        public DateTime HarvestedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Who performed the harvest</summary>
        [StringLength(200)]
        public string? HarvestedByUserId { get; set; }
    }
}
