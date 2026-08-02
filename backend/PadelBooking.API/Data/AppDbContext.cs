using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Models;

namespace PadelBooking.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Court> Courts { get; set; }

    public DbSet<Booking> Bookings { get; set; }

    public DbSet<Closure> Closures { get; set; }

    public DbSet<Offer> Offers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Booking>()
            .HasOne(booking => booking.Court)
            .WithMany(court => court.Bookings)
            .HasForeignKey(booking => booking.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Closure>()
            .HasOne(closure => closure.Court)
            .WithMany()
            .HasForeignKey(closure => closure.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasIndex(booking => new
            {
                booking.BookingDate,
                booking.CourtId,
                booking.BookingStatus
            });

        modelBuilder.Entity<Booking>()
            .HasIndex(booking => booking.Phone);

        modelBuilder.Entity<Closure>()
            .HasIndex(closure => new { closure.Date, closure.CourtId });
    }
}
