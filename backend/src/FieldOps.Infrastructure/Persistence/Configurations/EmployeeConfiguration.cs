using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        // Kimlik değeri veritabanı tarafından üretilir; kalıcılık kimliği domain oluşturma girdisi değildir.
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(employee => employee.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        // E-posta çalışan hesabı/iletişim kimliği kabul edilir; bu basit endüstri varsayımı veritabanında korunur.
        builder.Property(employee => employee.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(320)")
            .IsRequired();

        builder.HasIndex(employee => employee.Email)
            .IsUnique()
            .HasDatabaseName("ux_employees_email");

        builder.Property(employee => employee.CountryCode)
            .HasColumnName("country_code")
            .HasColumnType("varchar(2)")
            .IsRequired();
    }
}
