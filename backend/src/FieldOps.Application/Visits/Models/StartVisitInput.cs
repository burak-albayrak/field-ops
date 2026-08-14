namespace FieldOps.Application.Visits.Models;

// Bu HTTP request modeli değil, Visit başlatma use case'inin teknoloji bağımsız girdisidir.
public class StartVisitInput
{
    public StartVisitInput(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }
}
