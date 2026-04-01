using Microsoft.EntityFrameworkCore;
using TimeSlotter.Models;

namespace TimeSlotter.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Slot>()
            .Property(s => s.Status)
            .HasConversion<int>();

        modelBuilder.Entity<Provider>()
            .HasIndex(p => p.Slug)
            .IsUnique();
    }
}
