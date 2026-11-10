using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Features.Auth;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Features.Runner;

// Business logic for /api/runner: the signed-in runner's own profile,
// profile picture, password change and account deletion.
public sealed class RunnerService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    SupabaseStorageService storage)
{
    public const int MaxPictureBytes = 2 * 1024 * 1024;
    private const string UserNotFound = "User not found.";

    // ===================== View profile =====================

    // Reads only the columns the profile needs (never the password hash).
    public async Task<ProfileResponse> GetProfileAsync(string userId) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new ProfileResponse(u.Id, u.UserName, u.Email, u.DisplayName, u.Bio,
                u.ProfilePictureUrl, u.IsProfilePublic, u.CreatedAt))
            .SingleOrDefaultAsync()
        ?? throw ApiException.Unauthorized(UserNotFound);

    // ===================== Edit profile =====================

    public async Task<ProfileUpdatedResponse> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await FindUserAsync(userId);

        user.DisplayName = request.DisplayName.Trim();
        user.Bio = request.Bio?.Trim();
        user.IsProfilePublic = request.IsProfilePublic;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest,
                new ErrorResponse("Could not update profile.") { Errors = result.Errors.Select(e => e.Description) });

        return new ProfileUpdatedResponse("Profile updated successfully!", user.DisplayName, user.Bio, user.IsProfilePublic);
    }

    // ===================== Profile picture =====================

    // Uploads a new picture, saves its URL, then deletes the old picture.
    public async Task<ProfilePictureResponse> UploadProfilePictureAsync(string userId, IFormFile? file)
    {
        var user = await FindUserAsync(userId);

        if (file == null || file.Length == 0)
            throw ApiException.BadRequest("Please select an image.");

        if (file.Length > MaxPictureBytes)
            throw ApiException.BadRequest("Image must be smaller than 2 MB.");

        await using var stream = file.OpenReadStream();
        var contentType = await DetectImageTypeAsync(stream)
            ?? throw ApiException.BadRequest("Only valid JPEG, PNG and WebP images are allowed.");

        var sentType = file.ContentType?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(sentType) && sentType != contentType && sentType != "application/octet-stream")
            throw ApiException.BadRequest("Image format does not match its content type.");

        stream.Position = 0;

        string imageUrl;
        try
        {
            imageUrl = await storage.UploadProfilePictureAsync(stream, contentType, user.Id);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(StatusCodes.Status502BadGateway, new ErrorResponse("Unable to upload image to storage."));
        }

        var oldImageUrl = user.ProfilePictureUrl;
        user.ProfilePictureUrl = imageUrl;

        if (!(await userManager.UpdateAsync(user)).Succeeded)
        {
            await storage.TryDeleteProfilePictureAsync(imageUrl, user.Id); // Remove the unused upload.
            throw new ApiException(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Profile picture could not be updated."));
        }

        await storage.TryDeleteProfilePictureAsync(oldImageUrl, user.Id);
        return new ProfilePictureResponse("Profile picture updated successfully!", imageUrl);
    }

    public async Task<ProfilePictureResponse> RemoveProfilePictureAsync(string userId)
    {
        var user = await FindUserAsync(userId);

        var oldImageUrl = user.ProfilePictureUrl;
        if (string.IsNullOrWhiteSpace(oldImageUrl))
            throw ApiException.BadRequest("You do not have a profile picture.");

        // Clear the database reference first, then delete the file.
        user.ProfilePictureUrl = null;
        if (!(await userManager.UpdateAsync(user)).Succeeded)
            throw new ApiException(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Unable to remove profile picture."));

        await storage.TryDeleteProfilePictureAsync(oldImageUrl, user.Id);
        return new ProfilePictureResponse("Profile picture removed successfully.", null);
    }

    // ===================== Change password =====================

    // Signs the runner out everywhere, so they log in again with the new password.
    public async Task<MessageResponse> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = await db.LockUserAsync(userId) ?? throw ApiException.Unauthorized(UserNotFound);

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            throw ApiException.BadRequest("Current password is incorrect.");

        if (await userManager.CheckPasswordAsync(user, request.NewPassword))
            throw ApiException.BadRequest("New password must be different from your current password.");

        await PasswordHelper.ValidateAsync(userManager, user, request.NewPassword,
            "New password does not meet security requirements.");
        await PasswordHelper.SetNewPasswordAndSignOutAsync(db, userManager, user, request.NewPassword, DateTime.UtcNow);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MessageResponse("Password changed successfully. Please sign in again.");
    }

    // ===================== Delete account =====================

    // Related login records are removed by cascade delete in the database.
    public async Task<MessageResponse> DeleteAccountAsync(string userId, DeleteAccountRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = await db.LockUserAsync(userId) ?? throw ApiException.Unauthorized(UserNotFound);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            throw ApiException.BadRequest("Incorrect password. Account was not deleted.");

        var pictureUrl = user.ProfilePictureUrl;
        db.Users.Remove(user);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await storage.TryDeleteProfilePictureAsync(pictureUrl, userId);
        return new MessageResponse("Account deleted successfully.");
    }

    // ===================== Helpers =====================

    private async Task<ApplicationUser> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId) ?? throw ApiException.Unauthorized(UserNotFound);

    // Checks the file's first bytes (its "magic number"), not just its name,
    // so a renamed file cannot pretend to be an image.
    private static async Task<string?> DetectImageTypeAsync(Stream stream)
    {
        var header = new byte[12];
        if (await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false) < header.Length)
            throw ApiException.BadRequest("Invalid image file.");

        return ImageTypeFromHeader(header);
    }

    private static string? ImageTypeFromHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
            return "image/jpeg";

        if (bytes.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";

        if (bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }
}
