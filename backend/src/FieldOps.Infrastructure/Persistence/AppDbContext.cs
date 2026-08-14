using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Common.Exceptions;
using FieldOps.Domain.Entities;
using FieldOps.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FieldOps.Infrastructure.Persistence;

// AppDbContext, domain nesneleri ile ilişkisel veritabanı arasındaki Infrastructure sınırıdır;
// Domain katmanı EF Core veya PostgreSQL ayrıntılarını bilmeden kalır.
public class AppDbContext : DbContext, IUnitOfWork
{
    private const string ActiveVisitUniqueConstraint = "ux_visits_active_employee_store_planned_date";

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Visit> Visits => Set<Visit>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveVisitUniqueViolation(exception))
        {
            // Yalnızca bu partial unique index'in 23505 hatası use-case anlamındaki duplicate Visit'tir;
            // başka unique veya persistence hataları kendi özgün biçimleriyle yukarı taşınır.
            var visit = exception.Entries
                .Select(entry => entry.Entity)
                .OfType<Visit>()
                .First();

            throw new DuplicateVisitException(
                visit.EmployeeId,
                visit.StoreId,
                visit.PlannedDate,
                exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new StoreConfiguration());
        modelBuilder.ApplyConfiguration(new VisitConfiguration());
    }

    private static bool IsActiveVisitUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                ActiveVisitUniqueConstraint,
                StringComparison.Ordinal);
    }
}
