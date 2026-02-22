using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagingAgriculture.Models
{
    public class TaskAssignment
    {
        [Key]
        public int Id { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTime AssignedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public bool IsCompletedByEmployee { get; set; }
        public bool IsApprovedByBoss { get; set; }

        // Navigation
        public string AssignedToUserId { get; set; }
        [ForeignKey("AssignedToUserId")]
        public ApplicationUser? AssignedToUser { get; set; }

        public int? CompanyId { get; set; } // To link to company
        
        // Machine Reservation
        public int? AssignedMachineryId { get; set; }
        [ForeignKey("AssignedMachineryId")]
        public Machinery? AssignedMachinery { get; set; }
    }
}
