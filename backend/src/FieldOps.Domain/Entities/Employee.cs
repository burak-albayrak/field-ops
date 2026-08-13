namespace FieldOps.Domain.Entities;

public class Employee
{
    // Dışarıdan rastgele alan güncellenmesini önlemek için setter'lar özeldir;
    // ilerideki değişiklikler ancak açık bir domain davranışıyla yapılmalıdır.
    public long Id { get; private set; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string CountryCode { get; private set; }

    public Employee(string name, string email, string countryCode)
    {
        Name = name;
        Email = email;
        CountryCode = countryCode;
    }
}
