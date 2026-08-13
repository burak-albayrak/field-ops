namespace FieldOps.Domain.Entities;

public class Store
{
    // Mağazanın temel verileri yalnızca oluşturulurken atanır. Özel setter'lar,
    // kalıcılık veya üst katmanların nesneyi geçersiz bir ara duruma sokmasını engeller.
    public long Id { get; private set; }

    public string Name { get; private set; }

    public string CountryCode { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public Store(string name, string countryCode, double latitude, double longitude)
    {
        Name = name;
        CountryCode = countryCode;
        Latitude = latitude;
        Longitude = longitude;
    }
}
