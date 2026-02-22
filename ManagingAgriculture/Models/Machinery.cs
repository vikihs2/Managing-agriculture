using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
	/// <summary>
	/// Represents farm equipment/machinery (tractors, balers, etc.).
	/// Tracks purchase dates, service history, maintenance schedules, and operational status.
	/// </summary>
	public class Machinery
	{
		/// <summary>Primary key - unique identifier</summary>
		[Key]
		public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public string? OwnerUserId { get; set; }

		/// <summary>Name/model of the equipment (e.g., 'John Deere 5075E')</summary>
		[Required]
		[StringLength(100)]
		public string Name { get; set; } = string.Empty;

		/// <summary>Type of equipment (Tractor, Baler, Plow, etc.)</summary>
		[Required]
		[StringLength(50)]
		public string Type { get; set; } = string.Empty;

		/// <summary>Date equipment was purchased</summary>
		[Column(TypeName = "date")]
		public DateTime? PurchaseDate { get; set; }

		/// <summary>Current status: Excellent, Good, Fair, Poor, Needs Repair, etc.</summary>
		[StringLength(20)]
		public string Status { get; set; } = string.Empty;

		/// <summary>Purchase price in currency</summary>
		[Column(TypeName = "decimal(10,2)")]
		public decimal? PurchasePrice { get; set; }

		/// <summary>Date of last maintenance/service</summary>
		[Column(TypeName = "date")]
		public DateTime? LastServiceDate { get; set; }

		/// <summary>Scheduled date for next maintenance</summary>
		[Column(TypeName = "date")]
		public DateTime? NextServiceDate { get; set; }

		/// <summary>Record creation date</summary>
		public DateTime CreatedDate { get; set; }

		/// <summary>Last update date</summary>
		public DateTime UpdatedDate { get; set; }

		/// <summary>Engine hours (motor hours / operating hours) used instead of kilometers</summary>
		[Display(Name = "Engine Hours")]
		[Range(0, 9999999, ErrorMessage = "Engine hours cannot be negative")]
		[Column(TypeName = "decimal(10,1)")]
		public decimal? EngineHours { get; set; }

		public bool BoughtFromMarketplace { get; set; }

		// ===== NAVIGATION PROPERTIES =====

		/// <summary>Maintenance history records for this equipment</summary>
		public ICollection<MaintenanceHistory>? MaintenanceHistory { get; set; }

		/// <summary>Marketplace listings associated with this machinery</summary>
		public ICollection<MarketplaceListing>? MarketplaceListings { get; set; }
	}
}
