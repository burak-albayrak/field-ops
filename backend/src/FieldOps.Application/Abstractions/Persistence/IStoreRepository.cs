namespace FieldOps.Application.Abstractions.Persistence;

public interface IStoreRepository
{
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
}
