using System.Globalization;
using System.Text.Json;
using ILAQA.Models;

namespace ILAQA.Services
{
    // Calls Google Roads "snapToRoads" for poor GPS stretches (FR-12).
    // Disabled when no key is configured (GoogleRoads:ApiKey). On any
    // failure it returns no points, so the raw GPS is used unchanged.
    public class GoogleRoadsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleRoadsService> _logger;
        private readonly string? _apiKey;

        public GoogleRoadsService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GoogleRoadsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GoogleRoads:ApiKey"];
        }

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<List<SnappedRoadPoint>> SnapAsync(
            IReadOnlyList<RunGpsPoint> stretch,
            CancellationToken cancellationToken = default)
        {
            var snapped = new List<SnappedRoadPoint>();
            if (!IsEnabled || stretch.Count == 0)
                return snapped;

            string path = string.Join("|", stretch.Select(p =>
                p.Latitude.ToString("R", CultureInfo.InvariantCulture) + "," +
                p.Longitude.ToString("R", CultureInfo.InvariantCulture)));

            string url = "https://roads.googleapis.com/v1/snapToRoads" +
                "?interpolate=false" +
                "&path=" + Uri.EscapeDataString(path) +
                "&key=" + Uri.EscapeDataString(_apiKey!);

            try
            {
                using var response = await _httpClient.GetAsync(
                    url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Never log the URL: it contains the API key.
                    _logger.LogWarning(
                        "Google Roads returned HTTP {Status}.",
                        (int)response.StatusCode);
                    return snapped;
                }

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
                _logger.LogWarning(
                    "Google Roads snapping failed: {Error}", ex.GetType().Name);
                snapped.Clear();
            }

            return snapped;
        }
    }
}
