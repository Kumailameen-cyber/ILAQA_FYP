using System.Text.Json.Serialization;
using ILAQA.Features.Runs;

namespace ILAQA.Features.Public;

// What the unauthenticated guest website may receive (FR-24, FR-21, NFR-07).
// Deliberately no ids, user names, GPS points, routes or run data: only
// finished land, an owner colour and a display label.

// One territory on the public map. Holes are only sent when the land has any.
public sealed record PublicTerritoryDto(
    double AreaSquareMeters, string ColorHex, string OwnerLabel, bool IsShielded, List<LatLng> Boundary)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<List<LatLng>>? Holes { get; init; }
}

// One row of the public top list. No "is mine": nobody is signed in.
public sealed record PublicRankEntry(int Rank, string Name, double Value, string ColorHex);

// GET /api/public/summary: city totals and the top runners by area.
public sealed record PublicSummaryResponse(
    int RunnersWithLand, int TerritoryCount, double TotalAreaSquareMeters, List<PublicRankEntry> TopByArea);
