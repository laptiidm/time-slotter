using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Models;

namespace TimeSlotter.Data;

public class AppDbContext : IdentityDbContext<Provider, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Slot>(slot =>
        {
            slot.Property(s => s.ResourceContext)
                .IsRequired(false);

            slot.Property(s => s.AdminComment)
                .HasMaxLength(300)
                .IsRequired(false);

            slot.Property(s => s.Status)
                .HasConversion<int>();

            slot.Property(s => s.IsGrouped)
                .HasDefaultValue(false);

            slot.Property(s => s.RequiresApproval)
                .HasDefaultValue(false);
        });

        // Single-table strategy: Identity user store uses the Providers table name.
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("Providers");
            entity.Property(x => x.DefaultSlotIntervalMinutes)
                .HasDefaultValue(30);
            entity.HasIndex(p => p.Slug)
                .IsUnique();
        });

        modelBuilder.Entity<Booking>(b =>
        {
            // SQL Server: two FKs to Providers would create multiple cascade paths with SetNull.
            b.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.AssignedByProvider)
                .WithMany()
                .HasForeignKey(x => x.AssignedByProviderId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
