namespace FieldOps.Application.Common.Geography;

public static class HaversineDistanceCalculator
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static double CalculateMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        // Koordinatlar dereceyle gelir, ancak trigonometrik fonksiyonlar radyan kullanır.
        // Haversine, bilinen iki nokta arasındaki tekil mesafe için uygundur; mekansal arama veya indeks gerektirmez.
        var latitudeDifference = DegreesToRadians(latitude2 - latitude1);
        var longitudeDifference = DegreesToRadians(longitude2 - longitude1);
        var latitude1Radians = DegreesToRadians(latitude1);
        var latitude2Radians = DegreesToRadians(latitude2);

        var a = Math.Pow(Math.Sin(latitudeDifference / 2d), 2d)
            + Math.Cos(latitude1Radians)
            * Math.Cos(latitude2Radians)
            * Math.Pow(Math.Sin(longitudeDifference / 2d), 2d);
        var clampedA = Math.Clamp(a, 0d, 1d);
        var centralAngle = 2d * Math.Atan2(Math.Sqrt(clampedA), Math.Sqrt(1d - clampedA));

        return EarthRadiusMeters * centralAngle;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }
}
