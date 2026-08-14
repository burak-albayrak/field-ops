namespace FieldOps.Application.Common.Exceptions;

public class EmployeeNotFoundException : Exception
{
    public EmployeeNotFoundException(long employeeId)
        : base($"Employee {employeeId} was not found.")
    {
        EmployeeId = employeeId;
    }

    public long EmployeeId { get; }
}
