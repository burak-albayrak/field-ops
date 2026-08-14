using FieldOps.Domain.Enums;
using FieldOps.Domain.Exceptions;

namespace FieldOps.Domain.Entities;

public class Visit
{
    // Ziyaretin yaşam döngüsü alanları dış katmanlarca tek tek değiştirilemez.
    // Böylece her geçişin domain kurallarından geçmesi ve ilişkili verilerin birlikte
    // güncellenmesi Visit tarafından garanti edilir.
    public long Id { get; private set; }

    public long EmployeeId { get; private set; }

    public long StoreId { get; private set; }

    public DateOnly PlannedDate { get; private set; }

    public VisitStatus Status { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public double? StartLatitude { get; private set; }

    public double? StartLongitude { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // Sürüm yalnızca gerçek durum mutasyonlarında artırılır; ileride iyimser eşzamanlılık,
    // eski bir istemci durumunun daha yeni bir kararı ezdiğini bu değerle saptayacaktır.
    public long Version { get; private set; }

    public Visit(long employeeId, long storeId, DateOnly plannedDate, DateTime createdAt)
    {
        EnsureUtc(createdAt, nameof(createdAt));

        EmployeeId = employeeId;
        StoreId = storeId;
        PlannedDate = plannedDate;
        CreatedAt = createdAt;
        Status = VisitStatus.Planned;
        Version = 1;
        StartedAt = null;
        CompletedAt = null;
        StartLatitude = null;
        StartLongitude = null;
        Notes = null;
    }

    public void Start(DateTime startedAt, double latitude, double longitude)
    {
        EnsureUtc(startedAt, nameof(startedAt));
        EnsureCanStart();

        // 200 metre yakınlık kuralı mağaza koordinatına ihtiyaç duyar. Visit yalnızca
        // StoreId taşıdığı için bu kontrol, Store verisini yükleyen Application katmanında yapılacaktır.
        Status = VisitStatus.InProgress;
        StartedAt = startedAt;
        StartLatitude = latitude;
        StartLongitude = longitude;
        Version++;
    }

    // Saf guard, Application'ın imkansız bir işlem için Store/mesafe sorgusu yapmadan
    // Domain kuralını kontrol etmesini sağlar; gerçek geçiş ve Version değişimi yalnızca Start'ta kalır.
    public void EnsureCanStart()
    {
        if (Status != VisitStatus.Planned)
        {
            throw new InvalidVisitStateException(Status, "Start");
        }
    }

    // Complete tekrarını burada başarılı saymıyoruz: entity yalnızca gerçek durum
    // geçişlerini korur. İstemci tekrarları için idempotency kararı ve yan etkiler,
    // daha sonra Application katmanında ele alınacaktır.
    public void Complete(DateTime completedAt, string? notes)
    {
        EnsureUtc(completedAt, nameof(completedAt));

        if (Status != VisitStatus.InProgress)
        {
            throw new InvalidVisitStateException(Status, "Complete");
        }

        Status = VisitStatus.Completed;
        CompletedAt = completedAt;
        Notes = notes;
        Version++;
    }

    public void Cancel()
    {
        if (Status is not (VisitStatus.Planned or VisitStatus.InProgress))
        {
            throw new InvalidVisitStateException(Status, "Cancel");
        }

        Status = VisitStatus.Cancelled;
        Version++;
    }

    // Zaman damgaları gerçek dünyadaki tek bir anı temsil eder. UTC zorunluluğu,
    // farklı mağaza ve çalışan saat dilimlerinin kalıcı veride çelişki yaratmasını önler.
    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
        }
    }
}
