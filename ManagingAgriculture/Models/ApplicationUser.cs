using Microsoft.AspNetCore.Identity;

namespace ManagingAgriculture.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? CompanyId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        // Optionally add FirstName/LastName if you want better profile data
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        // HR Fields
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
        public decimal Salary { get; set; }
        public bool IsSalaryPaidInfo { get; set; } // Simple flag for "paid this month"

        public int LeaveDaysTotal { get; set; } = 20; // Default 20
        public int LeaveDaysUsed { get; set; }
        
        // Theme Preference
        public string? ThemePreference { get; set; } = "light"; // "light" or "dark"
    }
}
