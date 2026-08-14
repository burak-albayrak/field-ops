namespace FieldOps.Application.Abstractions.Persistence;

// Repository'ler yalnızca değişiklikleri hazırlar; use case tek bir SaveChanges sınırıyla kalıcılığı yönetir.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
