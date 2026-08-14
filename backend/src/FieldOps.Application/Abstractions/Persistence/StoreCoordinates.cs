namespace FieldOps.Application.Abstractions.Persistence;

// Başlatma mesafesi için Store'un tamamını yüklemek yerine yalnızca gerekli konum verisi taşınır.
public class StoreCoordinates
{
    public StoreCoordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }
}
