using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;

namespace ManagingAgriculture.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<CompanyInvitation> CompanyInvitations { get; set; }
        public DbSet<Field> Fields { get; set; }
        public DbSet<Plant> Plants { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<Machinery> Machinery { get; set; }
        public DbSet<MarketplaceListing> MarketplaceListings { get; set; }
        public DbSet<MarketplacePurchaseRequest> MarketplacePurchaseRequests { get; set; }
        public DbSet<ResourceUsage> ResourceUsages { get; set; }
        public DbSet<MaintenanceHistory> MaintenanceHistory { get; set; }
        public DbSet<Sensor> Sensors { get; set; }
        public DbSet<SensorReading> SensorReadings { get; set; }
        public DbSet<ContactForm> ContactForms { get; set; }
        public DbSet<TaskAssignment> TaskAssignments { get; set; }
        public DbSet<LeaveRecord> LeaveRecords { get; set;}
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<HarvestRecord> HarvestRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Field <-> Plant relationship
            builder.Entity<Plant>()
                .HasOne(p => p.Field)
                .WithMany(f => f.Plants)
                .HasForeignKey(p => p.FieldId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Field <-> Current Plant relationship
            builder.Entity<Field>()
                .HasOne(f => f.CurrentPlant)
                .WithOne()
                .HasForeignKey<Field>(f => f.CurrentPlantId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Machinery <-> MarketplaceListing relationship
            builder.Entity<MarketplaceListing>()
                .HasOne(m => m.Machinery)
                .WithMany(m => m.MarketplaceListings)
                .HasForeignKey(m => m.MachineryId)
                .OnDelete(DeleteBehavior.SetNull); // Or Restrict, depending on requirements. SetNull is safer if machinery is deleted but listing remains (though unlikely).

            // Configure decimal precision for Field
            builder.Entity<Field>()
                .Property(f => f.SizeInDecars)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Field>()
                .Property(f => f.AverageTemperatureCelsius)
                .HasColumnType("decimal(5,2)");

            // Configure decimal precision for currency and other decimal fields to avoid warnings
            builder.Entity<Resource>()
                .Property(r => r.Quantity)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Resource>()
                .Property(r => r.LowStockThreshold)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Machinery>()
                .Property(m => m.PurchasePrice)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Machinery>()
                .Property(m => m.EngineHours)
                .HasColumnType("decimal(10,1)");

            builder.Entity<MarketplaceListing>()
                .Property(m => m.SalePrice)
                .HasColumnType("decimal(10,2)");

            builder.Entity<MarketplaceListing>()
                .Property(m => m.RentalPricePerDay)
                .HasColumnType("decimal(10,2)");

            builder.Entity<MarketplaceListing>()
                .Property(m => m.EngineHours)
                .HasColumnType("decimal(10,1)");

            builder.Entity<ResourceUsage>()
                .Property(r => r.QuantityUsed)
                .HasColumnType("decimal(10,2)");

            builder.Entity<MaintenanceHistory>()
                .Property(m => m.Cost)
                .HasColumnType("decimal(10,2)");

            // Configure MarketplacePurchaseRequest relationships
            builder.Entity<MarketplacePurchaseRequest>()
                .HasOne(r => r.Listing)
                .WithMany(l => l.PurchaseRequests)
                .HasForeignKey(r => r.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MarketplacePurchaseRequest>()
                .HasOne(r => r.BuyerUser)
                .WithMany()
                .HasForeignKey(r => r.BuyerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure LeaveRequest relationships
            builder.Entity<LeaveRequest>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TaskAssignment FK to avoid multiple cascade paths
            builder.Entity<TaskAssignment>()
                .HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskAssignment>()
                .HasOne(t => t.AssignedByUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure HarvestRecord decimal precision
            builder.Entity<HarvestRecord>()
                .Property(h => h.EstimatedYieldKg)
                .HasColumnType("decimal(10,2)");

            builder.Entity<HarvestRecord>()
                .Property(h => h.FieldSizeDecars)
                .HasColumnType("decimal(10,2)");
        }
    }
}
