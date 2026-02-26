using System.ComponentModel.DataAnnotations;

namespace ManagingAgriculture.Models
{
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Register as a Company (Boss)")]
        public bool RegisterAsCompany { get; set; }

        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }
    }
}
