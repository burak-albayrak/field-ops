using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        // Koordinat aralıkları yapısal veri bütünlüğüdür; veri API dışından yazılsa bile PostgreSQL geçersiz değeri reddeder.
        builder.ToTable("stores", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_stores_latitude_range",
                "\"latitude\" >= -90 AND \"latitude\" <= 90");
            tableBuilder.HasCheckConstraint(
                "ck_stores_longitude_range",
                "\"longitude\" >= -180 AND \"longitude\" <= 180");
        });

        // Kimlik değeri veritabanı tarafından üretilir; kalıcılık kimliği domain oluşturma girdisi değildir.
        builder.HasKey(store => store.Id);
        builder.Property(store => store.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(store => store.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        // UK dahil proje terminolojisi dış bir standarda zorlanmadan iki karakterli kod olarak saklanır.
        builder.Property(store => store.CountryCode)
            .HasColumnName("country_code")
            .HasColumnType("varchar(2)")
            .IsRequired();

        // Ziyaret listeleri Store.CountryCode ile filtreleneceği için indeks sorgu şekline göre burada tutulur.
        builder.HasIndex(store => store.CountryCode)
            .HasDatabaseName("ix_stores_country_code");

        builder.Property(store => store.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(store => store.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("double precision")
            .IsRequired();
    }
}
