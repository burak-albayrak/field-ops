using FieldOps.Domain.Enums;

namespace FieldOps.Application.Visits.Models;

public class VisitDetailDto
{
    public VisitDetailDto(
        long id,
        EmployeeSummaryDto employee,
        StoreSummaryDto store,
        DateOnly plannedDate,
        VisitStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        double? startLatitude,
        double? startLongitude,
        string? notes,
        DateTime createdAt,
        long version)
    {
        Id = id;
        Employee = employee;
        Store = store;
        PlannedDate = plannedDate;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        StartLatitude = startLatitude;
        StartLongitude = startLongitude;
        Notes = notes;
        CreatedAt = createdAt;
        Version = version;
    }

    public long Id { get; }

    public EmployeeSummaryDto Employee { get; }

    public StoreSummaryDto Store { get; }

    public DateOnly PlannedDate { get; }

    public VisitStatus Status { get; }

    public DateTime? StartedAt { get; }

    public DateTime? CompletedAt { get; }

    public double? StartLatitude { get; }

    public double? StartLongitude { get; }

    public string? Notes { get; }

    public DateTime CreatedAt { get; }

    // İstemcinin ileride stale mutation'ı saptayabilmesi için Version DTO'da görünür kalır.
    public long Version { get; }
}
