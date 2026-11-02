using NetTopologySuite.Geometries;

namespace ILAQA.Models
{
    public class Territory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? RunId { get; set; }

        public int LoopIndex { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public Polygon Boundary { get; set; } = null!;

        public double AreaSquareMeters { get; set; }

        public DateTime? ShieldExpiresAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public Guid? MergedIntoTerritoryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}