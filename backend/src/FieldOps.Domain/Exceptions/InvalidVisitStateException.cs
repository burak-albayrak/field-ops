using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Exceptions;

/// <summary>
/// Ziyaretin mevcut durumundan istenen yaşam döngüsü işleminin yapılamadığını belirtir.
/// API katmanı daha sonra bu bilgiyi açıkça 409 Conflict yanıtına dönüştürebilir.
/// </summary>
public class InvalidVisitStateException : Exception
{
    public InvalidVisitStateException(VisitStatus currentStatus, string attemptedOperation)
        : base($"'{attemptedOperation}' operation cannot be performed while the visit is '{currentStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
    }

    public VisitStatus CurrentStatus { get; }

    public string AttemptedOperation { get; }
}
