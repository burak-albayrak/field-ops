using FieldOps.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly AppDbContext _context;

    public StoreRepository(AppDbContext context)
    {
        _context = context;
    }

    // Varlık kontrolü için entity yüklemek yerine AnyAsync yalnızca gerekli EXISTS sorgusunu üretir.
    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return _context.Stores.AnyAsync(store => store.Id == id, cancellationToken);
    }

    public Task<StoreCoordinates?> GetCoordinatesAsync(long id, CancellationToken cancellationToken = default)
    {
        return _context.Stores
            .AsNoTracking()
            .Where(store => store.Id == id)
            .Select(store => new StoreCoordinates(store.Latitude, store.Longitude))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
