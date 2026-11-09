using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ILAQA.Models;
using NetTopologySuite.Geometries;

namespace ILAQA.Features.Runs;

// ---------- Requests ----------

public sealed record StartRunRequest
{
    // Made once by the app and reused when an offline request is retried.
    [Required]
    public Guid RunId { get; init; }

    // When the runner pressed Start (UTC).
    [Required]
    public DateTime StartedAt { get; init; }
}

public sealed record RunGpsPointDto
{
    public int SequenceNo { get; init; }
    public int LegNo { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public DateTime RecordedAt { get; init; }
}

// Optional body of POST /api/runs/{id}/end (older apps send none).
// Both values are checked against the stored GPS before use.
public sealed record EndRunRequest
{
    // When Finish was pressed on the phone (UTC); keeps the real end time
    // of a Finish that was synced later.
    public DateTime? EndedAt { get; init; }

    // Running time measured on the phone, pauses excluded.
    public int? ActiveSeconds { get; init; }
}

// ---------- Responses ----------

public sealed record StartRunResponse(Guid Id, DateTime StartedAt, string Status, bool AlreadyExists);

// Pause and resume answer. ActiveStartedAt is only sent while running.
public sealed record RunStateResponse(
    Guid Id,
    string Status,
    int ActiveSeconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? ActiveStartedAt);

public sealed record LatLng(double Latitude, double Longitude);

public sealed record RunTerritoryDto(Guid TerritoryId, int LoopIndex, double AreaSquareMeters, List<LatLng> Boundary)
{
    public static RunTerritoryDto From(Territory t) =>
        new(t.Id, t.LoopIndex, Math.Round(t.AreaSquareMeters, 2), ToLatLngs(t.Boundary));

    // Database geometry stores X = longitude, Y = latitude.
    public static List<LatLng> ToLatLngs(Geometry boundary) =>
        boundary.Coordinates.Select(c => new LatLng(c.Y, c.X)).ToList();
}

// Every claim decision of a run, with the reason when rejected (FR-17).
public sealed record ClaimDecision(
    int StartSequenceNo, int EndSequenceNo, string Status, string? DecisionReason,
    double AreaSquareMeters, Guid? TerritoryId);

public sealed record UploadPointsResponse(
    Guid RunId, int Received, int Inserted, int AlreadyExisting, int NewlyClaimedCount,
    List<RunTerritoryDto> ConfirmedTerritories, List<ClaimDecision> ClaimDecisions);

public sealed record EndRunResponse(
    Guid Id, string Status, DateTime? EndedAt, int ActiveSeconds, string ValidationStatus,
    bool AlreadyCompleted, int DetectedTerritoryCount, List<RunTerritoryDto> DetectedTerritories,
    List<ClaimDecision> ClaimDecisions);

public sealed record MapTerritoryDto(
    Guid TerritoryId, double AreaSquareMeters, string ColorHex, string OwnerLabel,
    bool IsMine, bool IsShielded, List<LatLng> Boundary);

public sealed record RunDistanceResponse(
    Guid RunId, int TotalGpsPoints, int AcceptedSegments, int RejectedSegments,
    double CalculatedDistanceMeters, double CalculatedDistanceKm);

public sealed record RouteLegDto(int LegNo, List<LatLng> Coordinates);

public sealed record RouteDto(Guid RunId, DateTime StartedAt, List<RouteLegDto> Legs);
