namespace FieldOps.Application.Visits.Models;

// Bu HTTP request modeli değil, Create Visit use case'inin teknoloji bağımsız girdisidir.
public class CreateVisitInput
{
    public CreateVisitInput(long employeeId, long storeId, DateOnly plannedDate)
    {
        EmployeeId = employeeId;
        StoreId = storeId;
        PlannedDate = plannedDate;
    }

    public long EmployeeId { get; }

    public long StoreId { get; }

    public DateOnly PlannedDate { get; }
}
