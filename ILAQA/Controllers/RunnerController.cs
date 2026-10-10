
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ILAQA.Controllers
{
    [Route("api/runner")]
    [ApiController]
    [Authorize]
    public class RunnerController : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult GetMyProfile()
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var username = User.Identity?.Name;

            return Ok(new
            {
                message = "Protected API accessed successfully!",
                userId = userId,
                username = username
            });
        }
    }
}
