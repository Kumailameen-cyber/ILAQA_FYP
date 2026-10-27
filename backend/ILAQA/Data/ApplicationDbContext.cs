
using ILAQA.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<EmailVerificationOtp> EmailVerificationOtps
        { get; set; }

        public DbSet<PendingRegistration> PendingRegistrations
        { get; set; }
        public DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }
        public DbSet<Run> Runs { get; set; }
        public DbSet<RunGpsPoint> RunGpsPoints { get; set; }

        public DbSet<Territory> Territories { get; set; }
        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            // Keep ASP.NET Identity's existing database configuration.
            base.OnModelCreating(builder);
            builder.HasPostgresExtension("extensions", "postgis");
            builder.Entity<PendingRegistration>(entity =>
            {
                // Fast lookup and unique pending emails.
                entity.HasIndex(p => p.Email)
                    .IsUnique();

                // Fast lookup and unique pending usernames.
                entity.HasIndex(p => p.Username)
                    .IsUnique();

                // Faster cleanup of expired registrations.
                entity.HasIndex(p => p.ExpiresAt);
            });


            builder.Entity<PasswordResetOtp>(entity =>
            {
                // One active reset record per runner.
                entity.HasIndex(x => x.UserId)
                      .IsUnique();

                // Link reset OTP to the permanent Identity user.
                entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Run>(entity =>
            {
                // Every run belongs to one registered runner.
                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Fast lookup of a runner's run history.
                entity.HasIndex(r => new { r.UserId, r.StartedAt });

                // Use PostgreSQL timestamps with time zones.
                entity.Property(r => r.StartedAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(r => r.EndedAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(r => r.ActiveStartedAt)
                      .HasColumnType("timestamp with time zone");
            });

            builder.Entity<RunGpsPoint>(entity =>
            {
                // One run can contain thousands of GPS points.
                entity.HasOne(p => p.Run)
                      .WithMany(r => r.GpsPoints)
                      .HasForeignKey(p => p.RunId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Prevent duplicate sequence numbers within one run.
                entity.HasIndex(p => new { p.RunId, p.SequenceNo })
                      .IsUnique();

                // Efficient chronological GPS queries.
                entity.HasIndex(p => new { p.RunId, p.RecordedAt });

                entity.Property(p => p.RecordedAt)
                      .HasColumnType("timestamp with time zone");
            });
            builder.Entity<Territory>(entity =>
            {
                entity.HasOne<ApplicationUser>()
                      .WithMany()
                      .HasForeignKey(t => t.OwnerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(t => t.Boundary)
                      .HasColumnType("geometry(Polygon,4326)");

                entity.HasIndex(t => t.Boundary)
                      .HasMethod("gist");
                entity.Property(t => t.Status)
      .HasMaxLength(20);
            });
        }
    }
}
