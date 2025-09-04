using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Models;

namespace ResourceBooking.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Resource> Resources { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Resource entity
            builder.Entity<Resource>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Description).HasMaxLength(1000);
                entity.Property(r => r.Location).HasMaxLength(200);
                entity.Property(r => r.Capacity).IsRequired();
                entity.Property(r => r.IsAvailable).IsRequired();
                entity.Property(r => r.UnavailabilityReason).HasMaxLength(500);
                entity.Property(r => r.UnavailableUntil);
                entity.Property(r => r.UnavailabilityType);

                // Indexes
                entity.HasIndex(r => r.Name);
                entity.HasIndex(r => r.IsAvailable);
            });

            // Configure Booking entity
            builder.Entity<Booking>(entity =>
            {
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Purpose).IsRequired().HasMaxLength(500);
                entity.Property(b => b.StartTime).IsRequired();
                entity.Property(b => b.EndTime).IsRequired();
                entity.Property(b => b.Cancelled).IsRequired();
                entity.Property(b => b.UserId).IsRequired();
                entity.Property(b => b.ResourceId).IsRequired();
                entity.Property(b => b.IsRecurring).IsRequired();
                entity.Property(b => b.RecurrenceType);
                entity.Property(b => b.RecurrenceInterval);
                entity.Property(b => b.RecurrenceEndDate);
                entity.Property(b => b.ParentBookingId);

                // Indexes used by conflict checks and filters
                entity.HasIndex(b => new { b.ResourceId, b.StartTime, b.EndTime, b.Cancelled });
                entity.HasIndex(b => b.StartTime);

                // Configure relationships with NO ACTION to avoid cascade conflicts
                entity.HasOne(b => b.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(b => b.Resource)
                    .WithMany(r => r.Bookings)
                    .HasForeignKey(b => b.ResourceId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Self-referencing relationship for recurring bookings
                entity.HasOne(b => b.ParentBooking)
                    .WithMany(b => b.ChildBookings)
                    .HasForeignKey(b => b.ParentBookingId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure Notification entity
            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Type).IsRequired();
                entity.Property(n => n.IsRead).IsRequired();
                entity.Property(n => n.CreatedAt).IsRequired();
                entity.Property(n => n.UserId).IsRequired();

                // Indexes: speed up bell/Unread queries
                entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

                // Configure relationships with NO ACTION to avoid cascade conflicts
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(n => n.Booking)
                    .WithMany()
                    .HasForeignKey(n => n.BookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Seed some example data for development
            var resourceId1 = 1;
            var resourceId2 = 2;

            builder.Entity<Resource>().HasData(
                new Resource
                {
                    Id = resourceId1,
                    Name = "Conference Room A",
                    Description = "Main conference room with projector and video conferencing",
                    Location = "2nd Floor",
                    Capacity = 12,
                    IsAvailable = true
                },
                new Resource
                {
                    Id = resourceId2,
                    Name = "Company Vehicle",
                    Description = "Toyota Corolla - Blue",
                    Location = "Parking Bay 1",
                    Capacity = 4,
                    IsAvailable = true
                }
            );
        }
    }
}