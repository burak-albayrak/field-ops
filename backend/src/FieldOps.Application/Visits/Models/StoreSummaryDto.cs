namespace FieldOps.Application.Visits.Models;

public class StoreSummaryDto
{
    public StoreSummaryDto(long id, string name, string countryCode, double latitude, double longitude)
    {
        Id = id;
        Name = name;
        CountryCode = countryCode;
        Latitude = latitude;
        Longitude = longitude;
    }

    public long Id { get; }

    public string Name { get; }

    public string CountryCode { get; }

    public double Latitude { get; }

    public double Longitude { get; }
}
