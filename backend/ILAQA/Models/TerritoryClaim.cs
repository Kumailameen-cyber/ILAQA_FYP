using NetTopologySuite.Geometries;

namespace ILAQA.Models;

// One territory candidate from a run and its decision (ERD Module 6).
public class TerritoryClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    // GPS sequence range that forms this candidate.
    public int StartSequenceNo { get; set; }
    public int EndSequenceNo { get; set; }

    // Set when the claim created or changed a territory.
    public Guid? ResultTerritoryId { get; set; }

    public Polygon ProposedBoundary { get; set; } = null!;

    public double ProposedAreaSquareMeters { get; set; }

    public double? NewlyClaimedAreaSquareMeters { get; set; }

    // Approved or Rejected.
    public string Status { get; set; } = string.Empty;

    // Rejection reason code, e.g. VEHICLE_SPEED or TOO_NARROW.
    public string? DecisionReason { get; set; }

    public long XpAwarded { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? DecidedAt { get; set; }
}
