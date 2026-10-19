
using ILAQA.Data;
using ILAQA.DTOs;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Controllers
{
    [Route("api/runner")]
    [ApiController]
    [Authorize]
    public class RunnerController : ControllerBase
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SupabaseStorageService _storageService;
        public RunnerController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SupabaseStorageService storageService)
        {
            _userManager = userManager;
            _context = context;
            _storageService = storageService;
        }


        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            // Get the currently logged-in user from the database
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            // Return only safe profile information
            return Ok(new
            {
                id = user.Id,
                username = user.UserName,
                email = user.Email,
                displayName = user.DisplayName,
                bio = user.Bio,
                profilePictureUrl = user.ProfilePictureUrl,
                isProfilePublic = user.IsProfilePublic,
                createdAt = user.CreatedAt
            });
        }


        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateProfileRequest request)
        {
            // Find the currently logged-in user
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            // Update profile fields
            user.DisplayName = request.DisplayName.Trim();
            user.Bio = request.Bio?.Trim();
            user.IsProfilePublic = request.IsProfilePublic;

            // Save changes to the database
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Could not update profile.",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            return Ok(new
            {
                message = "Profile updated successfully!",
                displayName = user.DisplayName,
                bio = user.Bio,
                isProfilePublic = user.IsProfilePublic
            });
        }

        [HttpPost("me/profile-picture")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(2 * 1024 * 1024 + 65536)]
        public async Task<IActionResult> UploadProfilePicture(
            IFormFile file)
        {
            const int maxFileSize = 2 * 1024 * 1024;

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select an image."
                });
            }

            if (file.Length > maxFileSize)
            {
                return BadRequest(new
                {
                    message = "Image must be smaller than 2 MB."
                });
            }

            var allowedTypes = new[]
            {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new
                {
                    message = "Only JPEG, PNG and WebP images are allowed."
                });
            }

            await using var stream = file.OpenReadStream();

            // Check the actual image file signature.
            var header = new byte[12];
            var bytesRead = 0;

            while (bytesRead < header.Length)
            {
                var read = await stream.ReadAsync(
                    header.AsMemory(bytesRead, header.Length - bytesRead));

                if (read == 0) break;
                bytesRead += read;
            }

            if (bytesRead < 12)
            {
                return BadRequest(new
                {
                    message = "Invalid image file."
                });
            }

            bool isJpeg =
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            bool isPng =
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A;

            bool isWebp =
                header[0] == 0x52 &&
                header[1] == 0x49 &&
                header[2] == 0x46 &&
                header[3] == 0x46 &&
                header[8] == 0x57 &&
                header[9] == 0x45 &&
                header[10] == 0x42 &&
                header[11] == 0x50;

            bool validSignature = file.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => isJpeg,
                "image/png" => isPng,
                "image/webp" => isWebp,
                _ => false
            };

            if (!validSignature)
            {
                return BadRequest(new
                {
                    message = "Image format does not match its content type."
                });
            }

            stream.Position = 0;

            string imageUrl;

            try
            {
                imageUrl = await _storageService.UploadProfilePictureAsync(
                    stream,
                    file.ContentType.ToLowerInvariant(),
                    user.Id);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new
                {
                    message = "Unable to upload image to storage."
                });
            }

            // Remember the previous image before replacing it.
            var oldImageUrl = user.ProfilePictureUrl;

            user.ProfilePictureUrl = imageUrl;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                // The new image is not saved in the database.
                // Try to remove the unused upload.
                try
                {
                    await _storageService.DeleteProfilePictureAsync(
                        imageUrl, user.Id);
                }
                catch (HttpRequestException)
                {
                    // Cleanup failure should not hide the database error.
                }

                return StatusCode(500, new
                {
                    message = "Profile picture could not be updated."
                });
            }

            // Database update succeeded.
            // The new image is now the active profile picture.
            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                try
                {
                    await _storageService.DeleteProfilePictureAsync(
                        oldImageUrl, user.Id);
                }
                catch (HttpRequestException)
                {
                    // The new picture is already saved.
                    // A cleanup failure must not undo the update.
                    // In production, log this for later cleanup.
                }
            }

            return Ok(new
            {
                message = "Profile picture updated successfully!",
                profilePictureUrl = imageUrl
            });
        }

        [HttpDelete("me/profile-picture")]
        public async Task<IActionResult> RemoveProfilePicture()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            if (string.IsNullOrWhiteSpace(user.ProfilePictureUrl))
            {
                return BadRequest(new
                {
                    message = "You do not have a profile picture."
                });
            }

            var oldImageUrl = user.ProfilePictureUrl;

            // First, remove the picture reference from the database.
            user.ProfilePictureUrl = null;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return StatusCode(500, new
                {
                    message = "Unable to remove profile picture."
                });
            }

            // Then delete the actual image from Supabase Storage.
            try
            {
                await _storageService.DeleteProfilePictureAsync(
                    oldImageUrl,
                    user.Id);
            }
            catch (HttpRequestException)
            {
                // The profile picture has already been removed
                // from the user's profile.
                // Log this failure for a future cleanup retry.
            }

            return Ok(new
            {
                message = "Profile picture removed successfully.",
                profilePictureUrl = (string?)null
            });
        }



        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the user record while changing the password.
            var user = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            // Verify the current password.
            var correctPassword = await _userManager.CheckPasswordAsync(
                user, request.CurrentPassword);

            if (!correctPassword)
            {
                return BadRequest(new
                {
                    message = "Current password is incorrect."
                });
            }
            // Prevent using the same password again
            var samePassword = await _userManager.CheckPasswordAsync(
                user, request.NewPassword);

            if (samePassword)
            {
                return BadRequest(new
                {
                    message = "New password must be different from your current password."
                });
            }

            // Validate the new password using Identity's rules.
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(
                    _userManager, user, request.NewPassword);

                if (!validation.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "New password does not meet security requirements.",
                        errors = validation.Errors.Select(e => e.Description)
                    });
                }
            }

            var now = DateTime.UtcNow;

            // Hash and save the new password.
            user.PasswordHash = _userManager.PasswordHasher.HashPassword(
                user, request.NewPassword);

            // Invalidate existing access tokens.
            user.ActiveSessionId = null;

            // Update Identity's security stamp.
            user.SecurityStamp = Guid.NewGuid().ToString();

            // Revoke all refresh tokens.
            await _context.RefreshTokens
                .Where(x => x.UserId == user.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RevokedAt, now));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Password changed successfully. Please sign in again."
            });
        }

        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMyAccount(
            [FromBody] DeleteAccountRequest request)
        {
            // Identify the logged-in user.
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the account during deletion.
            var user = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found."
                });
            }

            // Confirm ownership using the current password.
            var validPassword = await _userManager.CheckPasswordAsync(
                user, request.Password);

            if (!validPassword)
            {
                return BadRequest(new
                {
                    message = "Incorrect password. Account was not deleted."
                });
            }


            // Remember the profile picture before deleting the user.
            var profilePictureUrl = user.ProfilePictureUrl;
            var deletedUserId = user.Id;

            // Remove the account.
            // Related authentication records use cascade deletion.
            _context.Users.Remove(user);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // The account is now permanently deleted from PostgreSQL.
            // Try to remove its profile picture from Supabase Storage.
            if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            {
                try
                {
                    await _storageService.DeleteProfilePictureAsync(
                        profilePictureUrl,
                        deletedUserId
                    );
                }
                catch (HttpRequestException)
                {
                    // Account deletion already succeeded.
                    // A storage cleanup failure must not undo it.
                    // TODO: Log this failure for a later cleanup retry.
                }
            }


            return Ok(new
            {
                message = "Account deleted successfully."
            });
        }



    }
}
