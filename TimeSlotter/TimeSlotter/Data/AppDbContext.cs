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

        // Single-table strategy: Identity user store uses the Providers table name.
        modelBuilder.Entity<Provider>().ToTable("Providers");

        modelBuilder.Entity<Slot>(slot =>
        {
            slot.Property(s => s.ResourceContext)
                .IsRequired(false);

            slot.Property(s => s.Status)
                .HasConversion<int>();
        });

        modelBuilder.Entity<Provider>()
            .HasIndex(p => p.Slug)
            .IsUnique();
    }
}
