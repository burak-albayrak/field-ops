using FieldOps.IntegrationTests.Infrastructure;
using Npgsql;

namespace FieldOps.IntegrationTests.Performance;

public sealed class PerformanceIndexTests : IntegrationTestBase
{
    private readonly PostgreSqlFixture _fixture;

    public PerformanceIndexTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Critical_completed_visit_index_has_expected_order_and_predicate()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                index_class.relname,
                index_metadata.indisunique,
                pg_get_indexdef(index_metadata.indexrelid),
                pg_get_expr(index_metadata.indpred, index_metadata.indrelid)
            FROM pg_index AS index_metadata
            JOIN pg_class AS table_class
                ON table_class.oid = index_metadata.indrelid
            JOIN pg_namespace AS table_namespace
                ON table_namespace.oid = table_class.relnamespace
            JOIN pg_class AS index_class
                ON index_class.oid = index_metadata.indexrelid
            WHERE
                table_namespace.nspname = 'public'
                AND table_class.relname = 'visits'
                AND index_class.relname = 'ix_visits_completed_employee_completed_at';
            """;

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("ix_visits_completed_employee_completed_at", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));

        var definition = reader.GetString(2);
        var predicate = reader.GetString(3);

        // Süre veya planner tercihi değil, sorgu şekline göre tasarlanan kalıcı şema sözleşmesi doğrulanır.
        Assert.Contains("(employee_id, completed_at DESC, id DESC)", definition);
        Assert.Contains("status", predicate);
        Assert.Contains("Completed", predicate);
        Assert.False(await reader.ReadAsync());
    }
}
