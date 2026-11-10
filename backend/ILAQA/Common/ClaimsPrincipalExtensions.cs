using System.Security.Claims;

namespace ILAQA.Common;

public static class ClaimsPrincipalExtensions
{
    // The signed-in runner's id, taken from the validated JWT.
    public static string GetRequiredUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId
            ? userId
            : throw ApiException.Unauthorized("User not found.");
}
