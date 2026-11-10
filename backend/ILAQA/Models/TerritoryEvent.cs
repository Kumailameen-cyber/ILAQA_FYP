using NetTopologySuite.Geometries;

namespace ILAQA.Models;

// Event types stored in TerritoryEvents.EventType.
public static class TerritoryEventTypes
{
    public const string Claimed = "Claimed";
    public const string TakenOver = "TakenOver";
    public const string Split = "Split";
    public const string ShieldRestored = "ShieldRestored";
    public const string BecameVulnerable = "BecameVulnerable";

    // Adjacent land of one owner combined into the older territory.
    public const string Merged = "Merged";
}

// History of territory changes: claims, takeovers, splits, merges
// (ERD Module 6).
public class TerritoryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TerritoryId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? RelatedTerritoryId { get; set; }

    public string? PreviousOwnerUserId { get; set; }

    public string? NewOwnerUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Geometry? BoundaryBefore { get; set; }

    public Geometry? BoundaryAfter { get; set; }

    public DateTime OccurredAt { get; set; }
}
