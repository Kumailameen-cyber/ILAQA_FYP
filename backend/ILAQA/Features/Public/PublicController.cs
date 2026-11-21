using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ILAQA.Features.Public;

// /api/public/* — the unauthenticated guest website (FR-24). GET only, no
// sign-in, limited per IP address. Thin controller: each endpoint only calls
// PublicService. Never returns ids, GPS points or routes (FR-21, NFR-07).
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting(PublicRules.RateLimitPolicy)]
public sealed class PublicController(PublicService publicData) : ControllerBase
{
    // All Active and Vulnerable land: boundary, holes, owner colour and label.
    [HttpGet("territories")]
    public async Task<List<PublicTerritoryDto>> GetTerritories() =>
        await publicData.GetTerritoriesAsync();

    // City totals and the top 10 runners by area.
    [HttpGet("summary")]
    public async Task<PublicSummaryResponse> GetSummary() =>
        await publicData.GetSummaryAsync();
}
