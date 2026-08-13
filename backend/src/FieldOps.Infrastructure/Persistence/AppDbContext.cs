using FieldOps.Domain.Entities;
using FieldOps.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence;

// AppDbContext, domain nesneleri ile ilişkisel veritabanı arasındaki Infrastructure sınırıdır;
// Domain katmanı EF Core veya PostgreSQL ayrıntılarını bilmeden kalır.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Visit> Visits => Set<Visit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new StoreConfiguration());
        modelBuilder.ApplyConfiguration(new VisitConfiguration());
    }
}
