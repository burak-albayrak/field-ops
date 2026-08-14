namespace FieldOps.Application.Visits.Models;

public class EmployeeSummaryDto
{
    public EmployeeSummaryDto(long id, string name, string email, string countryCode)
    {
        Id = id;
        Name = name;
        Email = email;
        CountryCode = countryCode;
    }

    public long Id { get; }

    public string Name { get; }

    public string Email { get; }

    public string CountryCode { get; }
}
