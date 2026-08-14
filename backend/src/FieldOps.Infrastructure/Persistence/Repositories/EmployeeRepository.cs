using FieldOps.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;

    public EmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    // Varlık kontrolü için entity yüklemek yerine AnyAsync yalnızca gerekli EXISTS sorgusunu üretir.
    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return _context.Employees.AnyAsync(employee => employee.Id == id, cancellationToken);
    }
}
