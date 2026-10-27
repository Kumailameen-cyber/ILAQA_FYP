
using System;

namespace ILAQA.Services
{
    public class GpsDistanceService
    {
        private const double EarthRadiusMeters = 6371000;

        // Calculates distance between two GPS coordinates.
        public double CalculateDistance(
            double lat1,
            double lon1,
            double lat2,
            double lon2)
        {
            double latitudeDifference =
                ToRadians(lat2 - lat1);

            double longitudeDifference =
                ToRadians(lon2 - lon1);

            double a =
                Math.Pow(Math.Sin(latitudeDifference / 2), 2)
                + Math.Cos(ToRadians(lat1))
                * Math.Cos(ToRadians(lat2))
                * Math.Pow(Math.Sin(longitudeDifference / 2), 2);

            double c = 2 * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(Math.Max(0, 1 - a)));

            return EarthRadiusMeters * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }
}
