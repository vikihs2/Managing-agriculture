using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
    /// <summary>
    /// Represents a leave request submitted by an employee or manager.
    /// The boss approves or rejects requests. Only one person can be off on any given date.
    /// </summary>
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime LeaveDate { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        /// <summary>Pending, Approved, Rejected</summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>When the request was submitted</summary>
        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When the boss decided</summary>
        public DateTime? DecidedDate { get; set; }

        /// <summary>Boss's note/comment</summary>
        [StringLength(500)]
        public string? BossNote { get; set; }
    }
}
