namespace ILAQA.Services;

// Distance in metres between two GPS coordinates (Haversine formula).
public sealed class GpsDistanceService
{
    private const double EarthRadiusMeters = 6371000;

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);

        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0, 1 - a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
