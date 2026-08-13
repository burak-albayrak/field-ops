using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FieldOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "varchar(200)", nullable: false),
                    email = table.Column<string>(type: "varchar(320)", nullable: false),
                    country_code = table.Column<string>(type: "varchar(2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "varchar(200)", nullable: false),
                    country_code = table.Column<string>(type: "varchar(2)", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.id);
                    table.CheckConstraint("ck_stores_latitude_range", "\"latitude\" >= -90 AND \"latitude\" <= 90");
                    table.CheckConstraint("ck_stores_longitude_range", "\"longitude\" >= -180 AND \"longitude\" <= 180");
                });

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    start_latitude = table.Column<double>(type: "double precision", nullable: true),
                    start_longitude = table.Column<double>(type: "double precision", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visits", x => x.id);
                    table.CheckConstraint("ck_visits_start_coordinates_pair", "(\"start_latitude\" IS NULL AND \"start_longitude\" IS NULL) OR (\"start_latitude\" IS NOT NULL AND \"start_longitude\" IS NOT NULL)");
                    table.CheckConstraint("ck_visits_start_latitude_range", "\"start_latitude\" IS NULL OR (\"start_latitude\" >= -90 AND \"start_latitude\" <= 90)");
                    table.CheckConstraint("ck_visits_start_longitude_range", "\"start_longitude\" IS NULL OR (\"start_longitude\" >= -180 AND \"start_longitude\" <= 180)");
                    table.ForeignKey(
                        name: "FK_visits_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visits_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_employees_email",
                table: "employees",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stores_country_code",
                table: "stores",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_visits_completed_employee_completed_at",
                table: "visits",
                columns: new[] { "employee_id", "completed_at", "id" },
                descending: new[] { false, true, true },
                filter: "\"status\" = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "ix_visits_employee_planned_date",
                table: "visits",
                columns: new[] { "employee_id", "planned_date", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_visits_store_planned_date",
                table: "visits",
                columns: new[] { "store_id", "planned_date", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_visits_active_employee_store_planned_date",
                table: "visits",
                columns: new[] { "employee_id", "store_id", "planned_date" },
                unique: true,
                filter: "\"status\" IN ('Planned', 'InProgress')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "stores");
        }
    }
}
