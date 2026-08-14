using FieldOps.Domain.Enums;

namespace FieldOps.Application.Visits.Models;

public class VisitListItemDto
{
    public VisitListItemDto(
        long id,
        long employeeId,
        string employeeName,
        long storeId,
        string storeName,
        string countryCode,
        DateOnly plannedDate,
        VisitStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        long version)
    {
        Id = id;
        EmployeeId = employeeId;
        EmployeeName = employeeName;
        StoreId = storeId;
        StoreName = storeName;
        CountryCode = countryCode;
        PlannedDate = plannedDate;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Version = version;
    }

    public long Id { get; }

    public long EmployeeId { get; }

    public string EmployeeName { get; }

    public long StoreId { get; }

    public string StoreName { get; }

    public string CountryCode { get; }

    public DateOnly PlannedDate { get; }

    public VisitStatus Status { get; }

    public DateTime? StartedAt { get; }

    public DateTime? CompletedAt { get; }

    // Liste sonucu da client'ın sonraki optimistic concurrency işlemleri için sürümü taşır.
    public long Version { get; }
}
