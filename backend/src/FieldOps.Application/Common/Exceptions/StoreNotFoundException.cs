namespace FieldOps.Application.Common.Exceptions;

public class StoreNotFoundException : Exception
{
    public StoreNotFoundException(long storeId)
        : base($"Store {storeId} was not found.")
    {
        StoreId = storeId;
    }

    public long StoreId { get; }
}
