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

        // Who the task is assigned to
        public string AssignedToUserId { get; set; } = string.Empty;
        [ForeignKey("AssignedToUserId")]
        public ApplicationUser? AssignedToUser { get; set; }

        // Who assigned this task (Boss or Manager)
        public string? AssignedByUserId { get; set; }
        [ForeignKey("AssignedByUserId")]
        public ApplicationUser? AssignedByUser { get; set; }

        public int? CompanyId { get; set; }
        
        // Machine Reservation
        public int? AssignedMachineryId { get; set; }
        [ForeignKey("AssignedMachineryId")]
        public Machinery? AssignedMachinery { get; set; }
    }
}
