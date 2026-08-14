namespace FieldOps.Application.Abstractions.Persistence;

// Persistence sözleşmesi Application'da kalır; Infrastructure bu ihtiyacı teknoloji ayrıntılarıyla uygular.
public interface IEmployeeRepository
{
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
}
