using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxFailedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTime>(
                name: "failed_at",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "id" },
                filter: "\"processed_at\" IS NULL AND \"failed_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "id" },
                filter: "\"processed_at\" IS NULL");
        }
    }
}
