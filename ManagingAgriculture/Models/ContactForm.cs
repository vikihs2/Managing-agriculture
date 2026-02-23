using System.ComponentModel.DataAnnotations;

namespace ManagingAgriculture.Models
{
    public class ContactForm
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        // Reply Fields
        public string? ReplyMessage { get; set; }
        public bool IsReplied { get; set; } = false;
        public DateTime? RepliedDate { get; set; }
        public string? RepliedBy { get; set; } // "Admin" or "IT" or UserID

        // Tracking for notification badge
        public bool IsReadByUser { get; set; } = false;
    }
}
