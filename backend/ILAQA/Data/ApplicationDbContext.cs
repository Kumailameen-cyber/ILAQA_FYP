
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

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            // Keep ASP.NET Identity's existing database configuration.
            base.OnModelCreating(builder);

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
        }
    }
}
