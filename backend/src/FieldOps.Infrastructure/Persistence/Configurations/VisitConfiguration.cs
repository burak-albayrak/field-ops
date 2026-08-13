using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        // Başlangıç koordinatları bir çift oluşturur ve varsa coğrafi aralıklarda olmalıdır.
        // 200 metre kuralı Store verisine bağlı iş kuralıdır; SQL constraint olarak burada uygulanmaz.
        builder.ToTable("visits", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_visits_start_coordinates_pair",
                "(\"start_latitude\" IS NULL AND \"start_longitude\" IS NULL) OR " +
                "(\"start_latitude\" IS NOT NULL AND \"start_longitude\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "ck_visits_start_latitude_range",
                "\"start_latitude\" IS NULL OR " +
                "(\"start_latitude\" >= -90 AND \"start_latitude\" <= 90)");
            tableBuilder.HasCheckConstraint(
                "ck_visits_start_longitude_range",
                "\"start_longitude\" IS NULL OR " +
                "(\"start_longitude\" >= -180 AND \"start_longitude\" <= 180)");
        });

        // Kimlik değeri veritabanı tarafından üretilir; kalıcılık kimliği domain oluşturma girdisi değildir.
        builder.HasKey(visit => visit.Id);
        builder.Property(visit => visit.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(visit => visit.EmployeeId)
            .HasColumnName("employee_id")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(visit => visit.StoreId)
            .HasColumnName("store_id")
            .HasColumnType("bigint")
            .IsRequired();

        // PlannedDate bir an değil mağazanın iş takvimindeki gündür; PostgreSQL date olarak saklanır.
        builder.Property(visit => visit.PlannedDate)
            .HasColumnName("planned_date")
            .HasColumnType("date")
            .IsRequired();

        // Sayısal enum sırası değişebilir; okunabilir string saklama hem güvenli hem elle incelemesi kolaydır.
        builder.Property(visit => visit.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasColumnType("varchar(20)")
            .IsRequired();

        // Oluşturma, başlama ve bitiş gerçek zaman anlarıdır; saat dilimi belirsizliğini önlemek için timestamptz kullanılır.
        builder.Property(visit => visit.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(visit => visit.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(visit => visit.StartLatitude)
            .HasColumnName("start_latitude")
            .HasColumnType("double precision");

        builder.Property(visit => visit.StartLongitude)
            .HasColumnName("start_longitude")
            .HasColumnType("double precision");

        // Notların iş ihtiyacındaki üst sınırı belirtilmediği için keyfi bir limit yerine text kullanılır.
        builder.Property(visit => visit.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(visit => visit.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // UPDATE yalnızca veritabanındaki Version, kaydın ilk okunduğu sürümse başarılı olmalıdır.
        builder.Property(visit => visit.Version)
            .HasColumnName("version")
            .HasColumnType("bigint")
            .IsConcurrencyToken()
            .IsRequired();

        // Aktif ziyaret benzersizliği eşzamanlı oluşturmalarda authoritative korumadır.
        // Completed ve Cancelled terminal olduğu için aynı gün yeni aktif ziyareti engellemez.
        builder.HasIndex(visit => new { visit.EmployeeId, visit.StoreId, visit.PlannedDate })
            .IsUnique()
            .HasDatabaseName("ux_visits_active_employee_store_planned_date")
            .HasFilter("\"status\" IN ('Planned', 'InProgress')");

        // İndeksler her kolona değil, gerçek liste sorgularının filtre ve sıralama şekline göre oluşturulur.
        builder.HasIndex(visit => new { visit.EmployeeId, visit.PlannedDate, visit.Id })
            .HasDatabaseName("ix_visits_employee_planned_date")
            .IsDescending(false, true, true);

        builder.HasIndex(visit => new { visit.StoreId, visit.PlannedDate, visit.Id })
            .HasDatabaseName("ix_visits_store_planned_date")
            .IsDescending(false, true, true);

        builder.HasIndex(visit => new { visit.EmployeeId, visit.CompletedAt, visit.Id })
            .HasDatabaseName("ix_visits_completed_employee_completed_at")
            .HasFilter("\"status\" = 'Completed'")
            .IsDescending(false, true, true);

        // Ülke filtresi Store tablosu üzerinden yapılır; Visit'e CountryCode kopyalayıp şemayı denormalize etmeyiz.

        // Ziyaret geçmişini korumak için ilgili çalışan veya mağaza silinince zincirleme silme yapılmaz.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(visit => visit.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(visit => visit.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
