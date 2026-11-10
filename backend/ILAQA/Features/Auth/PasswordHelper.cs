using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Features.Auth;

// Password rules shared by Register, Reset Password and Change Password.
public static class PasswordHelper
{
    // Checks the password against the Identity rules set in Program.cs
    // (8+ characters, upper case, lower case, digit).
    public static async Task ValidateAsync(
        UserManager<ApplicationUser> userManager, ApplicationUser user, string password, string message)
    {
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);

            if (!result.Succeeded)
                throw new ApiException(StatusCodes.Status400BadRequest,
                    new ErrorResponse(message) { Errors = result.Errors.Select(e => e.Description) });
        }
    }

    // Saves a new password and signs the runner out on every device:
    // the JWT session is cleared and all refresh tokens are revoked.
    // The caller saves and commits.
    public static async Task SetNewPasswordAndSignOutAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        ApplicationUser user, string newPassword, DateTime now)
    {
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, newPassword);
        user.ActiveSessionId = null;
        user.SecurityStamp = Guid.NewGuid().ToString();

        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
    }
}
