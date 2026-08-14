namespace FieldOps.Application.Abstractions.Persistence;

public interface IStoreRepository
{
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);

    Task<StoreCoordinates?> GetCoordinatesAsync(long id, CancellationToken cancellationToken = default);
}
