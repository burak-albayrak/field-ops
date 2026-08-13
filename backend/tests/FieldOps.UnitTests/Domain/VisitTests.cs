using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Domain.Exceptions;

namespace FieldOps.UnitTests.Domain;

public class VisitTests
{
    [Fact]
    public void Constructor_WithUtcCreatedAt_InitializesPlannedVisit()
    {
        // Arrange
        var plannedDate = new DateOnly(2026, 8, 20);
        var createdAt = Utc(2026, 8, 13, 9, 30);

        // Act
        var visit = new Visit(11, 22, plannedDate, createdAt);

        // Assert
        Assert.Equal(11, visit.EmployeeId);
        Assert.Equal(22, visit.StoreId);
        Assert.Equal(plannedDate, visit.PlannedDate);
        Assert.Equal(createdAt, visit.CreatedAt);
        Assert.Equal(VisitStatus.Planned, visit.Status);
        Assert.Equal(1, visit.Version);
        Assert.Null(visit.StartedAt);
        Assert.Null(visit.CompletedAt);
        Assert.Null(visit.StartLatitude);
        Assert.Null(visit.StartLongitude);
        Assert.Null(visit.Notes);
    }

    [Fact]
    public void Start_FromPlanned_SetsStartDetailsAndIncrementsVersion()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        var startedAt = Utc(2026, 8, 20, 8, 15);

        // Act
        visit.Start(startedAt, 41.0082, 28.9784);

        // Assert
        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.Equal(startedAt, visit.StartedAt);
        Assert.Equal(41.0082, visit.StartLatitude);
        Assert.Equal(28.9784, visit.StartLongitude);
        // Gerçek geçişlerdeki sürüm ilerlemesi, sonraki aşamada iyimser eşzamanlılık kontrolünün temelidir.
        Assert.Equal(2, visit.Version);
    }

    [Fact]
    public void Start_FromInProgress_ThrowsAndLeavesVisitUnchanged()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        var firstStartedAt = Utc(2026, 8, 20, 8, 15);
        visit.Start(firstStartedAt, 41.0082, 28.9784);

        // Act
        var exception = Assert.Throws<InvalidVisitStateException>(
            () => visit.Start(Utc(2026, 8, 20, 8, 30), 40.0, 29.0));

        // Assert
        Assert.Equal(VisitStatus.InProgress, exception.CurrentStatus);
        Assert.Equal("Start", exception.AttemptedOperation);
        // Geçersiz istek hiçbir alanı değiştirmemelidir; aksi halde hatalı bir istek geçerli kararı bozabilir.
        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.Equal(firstStartedAt, visit.StartedAt);
        Assert.Equal(2, visit.Version);
    }

    [Fact]
    public void Complete_FromInProgress_SetsCompletionDetailsAndIncrementsVersion()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784);
        var completedAt = Utc(2026, 8, 20, 9, 0);

        // Act
        visit.Complete(completedAt, "Raf düzenlemesi tamamlandı.");

        // Assert
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(completedAt, visit.CompletedAt);
        Assert.Equal("Raf düzenlemesi tamamlandı.", visit.Notes);
        Assert.Equal(3, visit.Version);
    }

    [Fact]
    public void Complete_FromPlanned_ThrowsAndLeavesVisitUnchanged()
    {
        // Arrange
        var visit = CreatePlannedVisit();

        // Act
        var exception = Assert.Throws<InvalidVisitStateException>(
            () => visit.Complete(Utc(2026, 8, 20, 9, 0), "Not"));

        // Assert
        Assert.Equal(VisitStatus.Planned, exception.CurrentStatus);
        Assert.Equal("Complete", exception.AttemptedOperation);
        Assert.Equal(VisitStatus.Planned, visit.Status);
        Assert.Equal(1, visit.Version);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ThrowsAndPreservesOriginalCompletion()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784);
        var originalCompletedAt = Utc(2026, 8, 20, 9, 0);
        visit.Complete(originalCompletedAt, "İlk not");

        // Act
        Assert.Throws<InvalidVisitStateException>(
            () => visit.Complete(Utc(2026, 8, 20, 9, 30), "Yeni not"));

        // Assert
        // Tekrarlanan Complete burada bilinçli olarak reddedilir; retry/idempotency Application katmanının sorumluluğudur.
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(originalCompletedAt, visit.CompletedAt);
        Assert.Equal("İlk not", visit.Notes);
        Assert.Equal(3, visit.Version);
    }

    [Fact]
    public void Cancel_FromPlanned_SetsCancelledAndIncrementsVersion()
    {
        // Arrange
        var visit = CreatePlannedVisit();

        // Act
        visit.Cancel();

        // Assert
        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal(2, visit.Version);
    }

    [Fact]
    public void Cancel_FromInProgress_SetsCancelledAndIncrementsVersion()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784);

        // Act
        visit.Cancel();

        // Assert
        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal(3, visit.Version);
    }

    [Fact]
    public void Cancel_FromCompleted_ThrowsAndPreservesCompletedVisit()
    {
        // Arrange
        var visit = CreateCompletedVisit();

        // Act
        var exception = Assert.Throws<InvalidVisitStateException>(() => visit.Cancel());

        // Assert
        Assert.Equal(VisitStatus.Completed, exception.CurrentStatus);
        Assert.Equal("Cancel", exception.AttemptedOperation);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(3, visit.Version);
    }

    [Fact]
    public void CancelledVisit_RejectsFurtherLifecycleOperations()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        visit.Cancel();

        // Act / Assert
        Assert.Throws<InvalidVisitStateException>(() => visit.Cancel());
        Assert.Throws<InvalidVisitStateException>(
            () => visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784));
        Assert.Throws<InvalidVisitStateException>(
            () => visit.Complete(Utc(2026, 8, 20, 9, 0), "Not"));

        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal(2, visit.Version);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcCreatedAt_ThrowsArgumentException(DateTimeKind kind)
    {
        // UTC ihlali erken reddedilir; kalıcı veride belirsiz veya yerel saatler farklı zaman dilimlerinde farklı anlara dönüşebilir.
        var createdAt = DateTime.SpecifyKind(new DateTime(2026, 8, 13, 9, 30, 0), kind);

        Assert.Throws<ArgumentException>(() => new Visit(11, 22, new DateOnly(2026, 8, 20), createdAt));
    }

    [Fact]
    public void Start_WithNonUtcTimestamp_ThrowsAndLeavesVisitPlanned()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        var localStartedAt = DateTime.SpecifyKind(new DateTime(2026, 8, 20, 8, 15, 0), DateTimeKind.Local);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => visit.Start(localStartedAt, 41.0082, 28.9784));
        Assert.Equal(VisitStatus.Planned, visit.Status);
        Assert.Equal(1, visit.Version);
    }

    [Fact]
    public void Complete_WithNonUtcTimestamp_ThrowsAndLeavesVisitInProgress()
    {
        // Arrange
        var visit = CreatePlannedVisit();
        visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784);
        var unspecifiedCompletedAt = DateTime.SpecifyKind(
            new DateTime(2026, 8, 20, 9, 0, 0), DateTimeKind.Unspecified);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => visit.Complete(unspecifiedCompletedAt, "Not"));
        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.Equal(2, visit.Version);
        Assert.Null(visit.CompletedAt);
    }

    private static Visit CreatePlannedVisit()
    {
        return new Visit(11, 22, new DateOnly(2026, 8, 20), Utc(2026, 8, 13, 9, 30));
    }

    private static Visit CreateCompletedVisit()
    {
        var visit = CreatePlannedVisit();
        visit.Start(Utc(2026, 8, 20, 8, 15), 41.0082, 28.9784);
        visit.Complete(Utc(2026, 8, 20, 9, 0), "Not");
        return visit;
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
