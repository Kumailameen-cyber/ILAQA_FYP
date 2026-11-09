using ILAQA.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ILAQA.Features.Runner;

// /api/runner — the signed-in runner's own account.
// Thin controller: each endpoint only calls RunnerService.
[ApiController]
[Route("api/runner")]
[Authorize]
public sealed class RunnerController(RunnerService runner) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ProfileResponse> GetMyProfile() =>
        await runner.GetProfileAsync(User.GetRequiredUserId());

    [HttpPut("me")]
    public async Task<ProfileUpdatedResponse> UpdateMyProfile(UpdateProfileRequest request) =>
        await runner.UpdateProfileAsync(User.GetRequiredUserId(), request);

    [HttpPost("me/profile-picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RunnerService.MaxPictureBytes + 65536)]
    public async Task<ProfilePictureResponse> UploadProfilePicture(IFormFile? file) =>
        await runner.UploadProfilePictureAsync(User.GetRequiredUserId(), file);

    [HttpDelete("me/profile-picture")]
    public async Task<ProfilePictureResponse> RemoveProfilePicture() =>
        await runner.RemoveProfilePictureAsync(User.GetRequiredUserId());

    [HttpPut("change-password")]
    public async Task<MessageResponse> ChangePassword(ChangePasswordRequest request) =>
        await runner.ChangePasswordAsync(User.GetRequiredUserId(), request);

    [HttpDelete("me")]
    public async Task<MessageResponse> DeleteMyAccount(DeleteAccountRequest request) =>
        await runner.DeleteAccountAsync(User.GetRequiredUserId(), request);
}
