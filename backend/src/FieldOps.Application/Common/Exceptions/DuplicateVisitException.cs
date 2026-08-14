namespace FieldOps.Application.Common.Exceptions;

public class DuplicateVisitException : Exception
{
    public DuplicateVisitException(
        long employeeId,
        long storeId,
        DateOnly plannedDate,
        Exception? innerException = null)
        : base("An active Visit already exists for this employee, store and planned date.", innerException)
    {
        EmployeeId = employeeId;
        StoreId = storeId;
        PlannedDate = plannedDate;
    }

    public long EmployeeId { get; }

    public long StoreId { get; }

    public DateOnly PlannedDate { get; }
}
