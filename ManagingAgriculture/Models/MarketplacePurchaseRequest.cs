using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
    /// <summary>
    /// Represents a purchase request sent by a buyer to a seller for a marketplace listing.
    /// The seller (boss of the owning company) reviews and approves/rejects.
    /// </summary>
    public class MarketplacePurchaseRequest
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The listing being requested</summary>
        [Required]
        public int ListingId { get; set; }
        [ForeignKey("ListingId")]
        public MarketplaceListing? Listing { get; set; }

        /// <summary>The buyer user ID</summary>
        [Required]
        public string BuyerUserId { get; set; } = string.Empty;
        [ForeignKey("BuyerUserId")]
        public ApplicationUser? BuyerUser { get; set; }

        /// <summary>Company of the buyer (if applicable)</summary>
        public int? BuyerCompanyId { get; set; }

        /// <summary>Display name/company name of buyer</summary>
        [StringLength(200)]
        public string BuyerName { get; set; } = string.Empty;

        /// <summary>When the request was made</summary>
        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Pending, Approved, Rejected</summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>Optional message from buyer</summary>
        [StringLength(500)]
        public string? Message { get; set; }
    }
}
