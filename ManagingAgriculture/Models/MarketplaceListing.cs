using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
	/// <summary>
	/// Represents a marketplace listing for buying, selling, or renting farm equipment/products.
	/// Tracks item details, pricing, seller info, and listing status.
	/// </summary>
	public class MarketplaceListing
	{
		/// <summary>Primary key - unique identifier</summary>
		[Key]
		public int Id { get; set; }

		/// <summary>Name of the item being listed (e.g., 'John Deere 6120M Tractor')</summary>
		[Required]
		[StringLength(100)]
		public string ItemName { get; set; } = string.Empty;

		/// <summary>Category: Tractors, Balers, Seeds, Equipment, etc.</summary>
		[Required]
		[StringLength(50)]
		public string Category { get; set; } = string.Empty;

		/// <summary>Condition: Excellent, Good, Fair, Poor, Like New, etc.</summary>
		[Required]
		[StringLength(20)]
		public string ConditionStatus { get; set; } = string.Empty;

		/// <summary>Detailed description of the item</summary>
		public string? Description { get; set; }

		/// <summary>Sale price (if item is for sale)</summary>
		[Column(TypeName = "decimal(10,2)")]
		public decimal? SalePrice { get; set; }

		/// <summary>Daily rental price (if item is for rent)</summary>
		[Column(TypeName = "decimal(10,2)")]
		public decimal? RentalPricePerDay { get; set; }

		/// <summary>Name of the person/farm selling/renting</summary>
		[Required]
		[StringLength(100)]
		public string SellerName { get; set; } = string.Empty;

		/// <summary>Seller's phone number for contact</summary>
		[Required]
		[StringLength(20)]
		public string SellerPhone { get; set; } = string.Empty;

		/// <summary>URL to an image of the item</summary>
		public string? ImageUrl { get; set; }

		/// <summary>Type of listing: Sale, Rent, Sale or Rent</summary>
		[Required]
		[StringLength(20)]
		public string ListingType { get; set; } = string.Empty;

		/// <summary>Status: Active, Sold, Expired, Deactivated, etc.</summary>
		[StringLength(20)]
		public string ListingStatus { get; set; } = "Active";

		/// <summary>Record creation date</summary>
		public DateTime CreatedDate { get; set; }

		/// <summary>Last update date</summary>
		public DateTime UpdatedDate { get; set; }

		/// <summary>Engine hours for machinery listings (motor hours / operating hours)</summary>
		[Display(Name = "Engine Hours")]
		[Range(0, 9999999, ErrorMessage = "Engine hours cannot be negative")]
		[Column(TypeName = "decimal(10,1)")]
		public decimal? EngineHours { get; set; }

		// ===== SELLER TRACKING =====

		/// <summary>User who created the listing</summary>
		public string? SellerUserId { get; set; }

		/// <summary>Company of seller (if from a company)</summary>
		public int? SellerCompanyId { get; set; }

		// ===== NAVIGATION PROPERTIES =====

		/// <summary>Foreign key for the associated Machinery (optional)</summary>
		public int? MachineryId { get; set; }

		/// <summary>The associated Machinery item</summary>
		[ForeignKey("MachineryId")]
		public Machinery? Machinery { get; set; }

		/// <summary>Purchase requests for this listing</summary>
		public ICollection<MarketplacePurchaseRequest>? PurchaseRequests { get; set; }
	}
}
