using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
	/// <summary>
	/// Represents a plant/crop in the agricultural system.
	/// Tracks planting dates, growth stages, harvest dates, and agronomic details.
	/// </summary>
	public class Plant
	{
		/// <summary>Primary key - unique identifier</summary>
		[Key]
		public int Id { get; set; }

		/// <summary>Company ownership (if part of a company)</summary>
		public int? CompanyId { get; set; }
		public Company? Company { get; set; }

		/// <summary>User ownership (if freelancer)</summary>
		public string? OwnerUserId { get; set; }

		/// <summary>Field where this plant is growing</summary>
		public int? FieldId { get; set; }
		[ForeignKey("FieldId")]
		public Field? Field { get; set; }

		/// <summary>Name of the plant (e.g., 'Tomato Garden A')</summary>
		[Required]
		[StringLength(100)]
		public string Name { get; set; } = string.Empty;

		/// <summary>Type of plant (e.g., 'Tomato', 'Corn') - LOCKED after creation</summary>
		[Required]
		[StringLength(50)]
		public string PlantType { get; set; } = string.Empty;

		/// <summary>Date when planted</summary>
		[Column(TypeName = "date")]
		public DateTime PlantedDate { get; set; }

		/// <summary>Current date for tracking progress (can be set by user to calculate growth %)</summary>
		[Column(TypeName = "date")]
		public DateTime? CurrentTrackingDate { get; set; }

		/// <summary>Growth stage percentage (0-100)</summary>
		[Range(0, 100)]
		public int GrowthStagePercent { get; set; }

		/// <summary>Next task to perform</summary>
		[StringLength(200)]
		public string? NextTask { get; set; }

		/// <summary>Additional notes</summary>
		public string? Notes { get; set; }

		/// <summary>Physical location (Field, Greenhouse, etc.)</summary>
		[StringLength(100)]
		public string? Location { get; set; }

		/// <summary>Status: Active, Harvested, Failed, etc.</summary>
		[StringLength(20)]
		public string Status { get; set; } = "Active";

		// ===== AGRONOMIC DETAILS =====

		/// <summary>Whether plant is grown indoors</summary>
		public bool IsIndoor { get; set; }

		/// <summary>Watering frequency in days</summary>
		[Range(0, 365)]
		public int? WateringFrequencyDays { get; set; }

		/// <summary>Record creation date</summary>
		public DateTime CreatedDate { get; set; }

		/// <summary>Last update date</summary>
		public DateTime UpdatedDate { get; set; }

		// ===== NAVIGATION PROPERTIES =====

		/// <summary>Resource usage records for this plant</summary>
		public ICollection<ResourceUsage>? ResourceUsages { get; set; }
	}
}
