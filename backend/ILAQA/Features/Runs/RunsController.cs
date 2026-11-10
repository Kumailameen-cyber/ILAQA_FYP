using ILAQA.Common;
using ILAQA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace ILAQA.Features.Runs;

// /api/runs — recording a run and the territory map.
// Thin controller: each endpoint only calls RunService.
[ApiController]
[Route("api/runs")]
[Authorize]
public sealed class RunsController(RunService runs) : ControllerBase
{
    // Start a run: 201 when created, 200 when the app retried the same start.
    [HttpPost("start")]
    public async Task<IActionResult> StartRun(StartRunRequest request)
    {
        var (run, created) = await runs.StartAsync(User.GetRequiredUserId(), request);
        return created ? StatusCode(StatusCodes.Status201Created, run) : Ok(run);
    }

    [HttpPost("{runId:guid}/pause")]
    public async Task<RunStateResponse> PauseRun(Guid runId) =>
        await runs.PauseAsync(User.GetRequiredUserId(), runId);

    [HttpPost("{runId:guid}/resume")]
    public async Task<RunStateResponse> ResumeRun(Guid runId) =>
        await runs.ResumeAsync(User.GetRequiredUserId(), runId);

    // Upload a batch of GPS points (limited per runner, NFR-05).
    [HttpPost("{runId:guid}/points")]
    [EnableRateLimiting(GpsUploadRateLimit.PolicyName)]
    public async Task<UploadPointsResponse> UploadGpsPoints(Guid runId, List<RunGpsPointDto>? points) =>
        await runs.UploadPointsAsync(User.GetRequiredUserId(), runId, points);

    // Finish the run. The body is optional (older apps send none).
    [HttpPost("{runId:guid}/end")]
    public async Task<EndRunResponse> EndRun(
        Guid runId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EndRunRequest? request = null) =>
        await runs.EndAsync(User.GetRequiredUserId(), runId, request);

    // Server-side distance check of a run's stored GPS.
    [HttpGet("{runId:guid}/distance")]
    public async Task<RunDistanceResponse> GetRunDistance(Guid runId) =>
        await runs.GetDistanceAsync(User.GetRequiredUserId(), runId);

    // The runner's finished routes.
    [HttpGet("routes")]
    public async Task<List<RouteDto>> GetMySavedRoutes() =>
        await runs.GetSavedRoutesAsync(User.GetRequiredUserId());

    // Every visible territory for the map.
    [HttpGet("territories")]
    public async Task<List<MapTerritoryDto>> GetMapTerritories() =>
        await runs.GetMapTerritoriesAsync(User.GetRequiredUserId());
}
