
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
        public DbSet<TerritoryClaim> TerritoryClaims { get; set; }
        public DbSet<TerritoryEvent> TerritoryEvents { get; set; }
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
                // Link each territory to the run that created it.
                entity.HasOne<Run>()
                      .WithMany()
                      .HasForeignKey(t => t.RunId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Prevent the same run from saving a loop twice.
                entity.HasIndex(t => new { t.RunId, t.LoopIndex })
                      .IsUnique();
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

            builder.Entity<TerritoryClaim>(entity =>
            {
                entity.HasOne<Run>()
                      .WithMany()
                      .HasForeignKey(c => c.RunId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Territory>()
                      .WithMany()
                      .HasForeignKey(c => c.ResultTerritoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // ERD index.
                entity.HasIndex(c => new { c.RunId, c.SubmittedAt });

                // Fast lookup of a run's earlier decisions. Not unique: two
                // different areas can share a GPS range. Duplicates are
                // prevented by the planner under the run's row lock.
                entity.HasIndex(c => new
                {
                    c.RunId,
                    c.StartSequenceNo,
                    c.EndSequenceNo
                });

                entity.Property(c => c.ProposedBoundary)
                      .HasColumnType("geometry(Polygon,4326)");

                entity.Property(c => c.Status)
                      .HasMaxLength(20);

                entity.Property(c => c.SubmittedAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(c => c.DecidedAt)
                      .HasColumnType("timestamp with time zone");
            });

            builder.Entity<TerritoryEvent>(entity =>
            {
                entity.HasOne<Territory>()
                      .WithMany()
                      .HasForeignKey(e => e.TerritoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<TerritoryClaim>()
                      .WithMany()
                      .HasForeignKey(e => e.ClaimId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Territory>()
                      .WithMany()
                      .HasForeignKey(e => e.RelatedTerritoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<ApplicationUser>()
                      .WithMany()
                      .HasForeignKey(e => e.PreviousOwnerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<ApplicationUser>()
                      .WithMany()
                      .HasForeignKey(e => e.NewOwnerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.TerritoryId, e.OccurredAt });

                entity.Property(e => e.EventType)
                      .HasMaxLength(30);

                entity.Property(e => e.BoundaryBefore)
                      .HasColumnType("geometry(Geometry,4326)");

                entity.Property(e => e.BoundaryAfter)
                      .HasColumnType("geometry(Geometry,4326)");

                entity.Property(e => e.OccurredAt)
                      .HasColumnType("timestamp with time zone");
            });
        }
    }
}
