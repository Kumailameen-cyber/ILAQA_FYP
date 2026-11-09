using System.Globalization;
using System.Text.Json;
using ILAQA.Models;

namespace ILAQA.Services;

// Calls Google Roads "snapToRoads" for poor GPS stretches (FR-12).
// Disabled when no key is configured (GoogleRoads:ApiKey). A failed
// request returns null ("not checked"), never an empty "nothing to
// snap" answer, so the points are tried again later. After a failure
// Google is not called again for a cooldown (default 60 s), so an
// outage does not cost one request per upload.
public sealed class GoogleRoadsService(
    HttpClient httpClient, IConfiguration configuration, ILogger<GoogleRoadsService> logger)
{
    private const int DefaultFailureCooldownSeconds = 60;

    // Shared by all requests: the client itself is created per request.
    private static long _lastFailureTicks;

    private readonly string? _apiKey = configuration["GoogleRoads:ApiKey"];
    private readonly TimeSpan _failureCooldown = TimeSpan.FromSeconds(
        configuration.GetValue("GoogleRoads:FailureCooldownSeconds", DefaultFailureCooldownSeconds));

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    // Snapped points, an empty list when Google placed nothing, or null
    // when the request failed or is paused after a recent failure.
    public async Task<List<SnappedRoadPoint>?> SnapAsync(
        IReadOnlyList<RunGpsPoint> stretch,
        CancellationToken cancellationToken = default)
    {
        var snapped = new List<SnappedRoadPoint>();
        if (!IsEnabled || stretch.Count == 0)
            return snapped;

        long lastFailure = Interlocked.Read(ref _lastFailureTicks);
        if (lastFailure != 0 &&
            DateTime.UtcNow < new DateTime(lastFailure) + _failureCooldown)
            return null;

        string path = string.Join("|", stretch.Select(p =>
            p.Latitude.ToString("R", CultureInfo.InvariantCulture) + "," +
            p.Longitude.ToString("R", CultureInfo.InvariantCulture)));

        string url = "https://roads.googleapis.com/v1/snapToRoads" +
            "?interpolate=false" +
            "&path=" + Uri.EscapeDataString(path) +
            "&key=" + Uri.EscapeDataString(_apiKey!);

        try
        {
            using var response = await httpClient.GetAsync(
                url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Never log the URL: it contains the API key.
                logger.LogWarning(
                    "Google Roads returned HTTP {Status}.",
                    (int)response.StatusCode);

                int status = (int)response.StatusCode;
                if (status >= 500 || status == 429)
                {
                    // Outage or quota: retry later, pause meanwhile.
                    PauseAfterFailure();
                    return null;
                }

                // Rejected for good (e.g. 400 for this stretch): keep
                // the raw GPS and do not ask again.
                return snapped;
            }

            // Google answered: end any pause from an earlier failure.
            Interlocked.Exchange(ref _lastFailureTicks, 0);

            await using var body = await response.Content
                .ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(
                body, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty(
                    "snappedPoints", out var points))
                return snapped;

            foreach (var point in points.EnumerateArray())
            {
                if (!point.TryGetProperty("originalIndex", out var index) ||
                    !point.TryGetProperty("location", out var location))
                    continue;

                snapped.Add(new SnappedRoadPoint
                {
                    OriginalIndex = index.GetInt32(),
                    Latitude = location.GetProperty("latitude").GetDouble(),
                    Longitude = location.GetProperty("longitude").GetDouble()
                });
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is KeyNotFoundException ||
            ex is InvalidOperationException)
        {
            logger.LogWarning(
                "Google Roads snapping failed: {Error}", ex.GetType().Name);
            PauseAfterFailure();
            return null;
        }

        return snapped;
    }

    private static void PauseAfterFailure() =>
        Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
}
