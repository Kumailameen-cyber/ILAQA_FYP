using ILAQA.Models;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Data;

// PostgreSQL row locks used to stop two requests changing the same data at
// once (for example two logins, or an upload racing a Finish).
// EF Core LINQ cannot express "SELECT ... FOR UPDATE" or advisory locks, so
// this file is the ONLY place in the app with SQL text (20 lock calls in the
// services use these 6 methods). Every value is passed as a parameter
// (FromSqlInterpolated), so it is safe from SQL injection.
// Each method must be called inside a transaction; the lock is released
// when that transaction commits or rolls back.
public static class DbLocks
{

    public static Task<ApplicationUser?> LockUserAsync(this ApplicationDbContext db, string userId) =>
        db.Users
            .FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync();

    public static Task<PendingRegistration?> LockPendingRegistrationAsync(this ApplicationDbContext db, string email) =>
        db.PendingRegistrations
            .FromSqlInterpolated($"SELECT * FROM \"PendingRegistrations\" WHERE \"Email\" = {email} FOR UPDATE")
            .SingleOrDefaultAsync();

    public static Task<PasswordResetOtp?> LockPasswordResetAsync(this ApplicationDbContext db, string userId) =>
        db.PasswordResetOtps
            .FromSqlInterpolated($"SELECT * FROM \"PasswordResetOtps\" WHERE \"UserId\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync();

    // A run of this runner (API requests).
    public static Task<Run?> LockRunAsync(this ApplicationDbContext db, Guid runId, string userId) =>
        db.Runs
            .FromSqlInterpolated($"SELECT * FROM \"Runs\" WHERE \"Id\" = {runId} AND \"UserId\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync();

    // A run whoever owns it (background jobs).
    public static Task<Run?> LockAnyRunAsync(this ApplicationDbContext db, Guid runId, CancellationToken cancellationToken) =>
        db.Runs
            .FromSqlInterpolated($"SELECT * FROM \"Runs\" WHERE \"Id\" = {runId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    // Global lock shared by all territory changes (claims, takeovers, the
    // hourly vulnerability job), so overlapping claims can never both win (FR-20).
    // Fixed text with no user input, so it cannot be used for SQL injection.
    public static Task LockTerritoriesAsync(this ApplicationDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7421001)", cancellationToken);
}
