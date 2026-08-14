using System.Text.Json;
using FieldOps.Application.Abstractions.Outbox;

namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxWriter : IOutboxWriter
{
    public const string VisitCompletedType = "VisitCompleted";

    private readonly AppDbContext _context;

    public OutboxWriter(AppDbContext context)
    {
        _context = context;
    }

    public void AddVisitCompleted(
        long visitId,
        long employeeId,
        long storeId,
        DateTime completedAt)
    {
        // Payload tamamlanma anında immutable snapshot olarak alınır; gelecekteki worker Visit'i yeniden yüklemek zorunda kalmaz.
        var payload = JsonSerializer.Serialize(new
        {
            type = VisitCompletedType,
            visitId,
            employeeId,
            storeId,
            completedAt
        });

        var message = new OutboxMessage(VisitCompletedType, payload, completedAt);

        // Writer bağımsız SaveChanges çağırmaz; Visit güncellemesiyle aynı scoped DbContext ve tek commit sınırında kalır.
        _context.OutboxMessages.Add(message);
    }
}
