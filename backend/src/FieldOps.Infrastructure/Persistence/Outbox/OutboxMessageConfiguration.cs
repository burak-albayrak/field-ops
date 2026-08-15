using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.NextAttemptAt)
            .HasColumnName("next_attempt_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");

        // Worker yalnızca işlenmemiş ve permanent başarısız olmayan mesajları zaman ve Id sırasıyla okur.
        builder.HasIndex(message => new { message.NextAttemptAt, message.Id })
            .HasDatabaseName("ix_outbox_messages_pending_next_attempt")
            .HasFilter("\"processed_at\" IS NULL AND \"failed_at\" IS NULL");
    }
}
