using System.Net;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.IntegrationTests.Infrastructure;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitListTests : IntegrationTestBase
{
    public VisitListTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task List_returns_all_items_with_default_pagination_and_string_statuses()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync();
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(7, items.Length);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.False(root.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(seeded.MixedOrder, GetIds(root));
        Assert.All(items, item => Assert.Equal(JsonValueKind.String, item.GetProperty("status").ValueKind));
    }

    [Fact]
    public async Task List_filters_by_employee()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync($"?employeeId={seeded.EmployeeAId}");
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(4, items.Length);
        Assert.All(items, item => Assert.Equal(seeded.EmployeeAId, item.GetProperty("employeeId").GetInt64()));
    }

    [Fact]
    public async Task List_filters_by_store()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync($"?storeId={seeded.DeStoreId}");
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Equal(seeded.DeStoreId, item.GetProperty("storeId").GetInt64()));
    }

    [Fact]
    public async Task List_binds_completed_status_and_sorts_by_completed_time_then_id()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync("?status=Completed");
        var root = document.RootElement;

        Assert.Equal(
            new[] { seeded.CompletedSameTimeHigherId, seeded.CompletedNewestId, seeded.CompletedOldestId },
            GetIds(root));
        Assert.All(
            root.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("Completed", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task List_filters_country_code_through_store_relation()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync("?countryCode=TR");
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(4, items.Length);
        Assert.All(items, item => Assert.Equal("TR", item.GetProperty("countryCode").GetString()));
        Assert.DoesNotContain(seeded.UkPlannedId, items.Select(item => item.GetProperty("id").GetInt64()));
    }

    [Fact]
    public async Task List_applies_inclusive_planned_date_boundaries()
    {
        await ArrangeListDataAsync();

        using var startOnly = await GetListAsync("?startDate=2026-08-13");
        Assert.All(
            GetPlannedDates(startOnly.RootElement),
            date => Assert.True(date >= new DateOnly(2026, 8, 13)));

        using var endOnly = await GetListAsync("?endDate=2026-08-11");
        Assert.All(
            GetPlannedDates(endOnly.RootElement),
            date => Assert.True(date <= new DateOnly(2026, 8, 11)));

        using var both = await GetListAsync("?startDate=2026-08-10&endDate=2026-08-12");
        var dates = GetPlannedDates(both.RootElement);
        Assert.Contains(new DateOnly(2026, 8, 10), dates);
        Assert.Contains(new DateOnly(2026, 8, 12), dates);
        Assert.All(dates, date => Assert.InRange(date, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12)));
    }

    [Fact]
    public async Task List_combines_employee_country_status_and_date_filters()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync(
            $"?employeeId={seeded.EmployeeAId}&countryCode=TR&status=Completed" +
            "&startDate=2026-08-10&endDate=2026-08-12");

        Assert.Equal(
            new[] { seeded.CompletedNewestId, seeded.CompletedOldestId },
            GetIds(document.RootElement));
    }

    [Fact]
    public async Task List_sorts_explicit_planned_status_by_planned_date_then_id()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync("?status=Planned");

        Assert.Equal(
            new[] { seeded.TrPlannedId, seeded.UkPlannedId },
            GetIds(document.RootElement));
    }

    [Fact]
    public async Task List_sorts_mixed_results_with_completed_group_first()
    {
        var seeded = await ArrangeListDataAsync();

        using var document = await GetListAsync();

        Assert.Equal(seeded.MixedOrder, GetIds(document.RootElement));
    }

    [Fact]
    public async Task List_paginates_without_duplicates_and_reports_has_next_page()
    {
        var seeded = await ArrangeListDataAsync();

        using var first = await GetListAsync("?page=1&pageSize=2");
        using var second = await GetListAsync("?page=2&pageSize=2");
        using var third = await GetListAsync("?page=3&pageSize=2");
        using var fourth = await GetListAsync("?page=4&pageSize=2");

        Assert.True(first.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.True(second.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.True(third.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.False(fourth.RootElement.GetProperty("hasNextPage").GetBoolean());

        var allIds = GetIds(first.RootElement)
            .Concat(GetIds(second.RootElement))
            .Concat(GetIds(third.RootElement))
            .Concat(GetIds(fourth.RootElement))
            .ToArray();

        Assert.Equal(seeded.MixedOrder, allIds);
        Assert.Equal(allIds.Length, allIds.Distinct().Count());
        Assert.Single(GetIds(fourth.RootElement));
    }

    [Fact]
    public async Task List_returns_empty_success_when_no_visits_match()
    {
        await ArrangeListDataAsync();

        using var document = await GetListAsync("?employeeId=999999");
        var root = document.RootElement;

        Assert.Empty(root.GetProperty("items").EnumerateArray());
        Assert.False(root.GetProperty("hasNextPage").GetBoolean());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?employeeId=0")]
    [InlineData("?storeId=0")]
    [InlineData("?status=DefinitelyNotAStatus")]
    [InlineData("?startDate=2026-08-12&endDate=2026-08-10")]
    public async Task List_returns_validation_problem_for_invalid_queries(string query)
    {
        using var response = await Client.GetAsync($"/api/visits{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_error", document.RootElement.GetProperty("code").GetString());
    }

    private async Task<SeededVisits> ArrangeListDataAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var employeeA = new Employee("Employee A", "employee-a@example.com", "TR");
            var employeeB = new Employee("Employee B", "employee-b@example.com", "DE");
            var trStore = new Store("TR Store", "TR", 39.9334, 32.8597);
            var deStore = new Store("DE Store", "DE", 52.52, 13.405);
            var ukStore = new Store("UK Store", "UK", 51.5074, -0.1278);
            context.Employees.AddRange(employeeA, employeeB);
            context.Stores.AddRange(trStore, deStore, ukStore);
            await context.SaveChangesAsync();

            var completedOldest = CreateCompletedVisit(
                employeeA.Id,
                trStore.Id,
                new DateOnly(2026, 8, 10),
                new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc));
            var completedNewest = CreateCompletedVisit(
                employeeA.Id,
                trStore.Id,
                new DateOnly(2026, 8, 12),
                new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc));
            var completedSameTimeHigherId = CreateCompletedVisit(
                employeeB.Id,
                deStore.Id,
                new DateOnly(2026, 8, 11),
                new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc));
            var trPlanned = CreateVisit(employeeA.Id, trStore.Id, new DateOnly(2026, 8, 14));
            var ukPlanned = CreateVisit(employeeB.Id, ukStore.Id, new DateOnly(2026, 8, 12));
            var inProgress = CreateVisit(employeeA.Id, deStore.Id, new DateOnly(2026, 8, 13));
            inProgress.Start(
                new DateTime(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc),
                52.5201,
                13.4051);
            var cancelled = CreateVisit(employeeB.Id, trStore.Id, new DateOnly(2026, 8, 11));
            cancelled.Cancel();

            context.Visits.AddRange(
                completedOldest,
                completedNewest,
                completedSameTimeHigherId,
                trPlanned,
                ukPlanned,
                inProgress,
                cancelled);
            await context.SaveChangesAsync();

            return new SeededVisits(
                employeeA.Id,
                deStore.Id,
                completedOldest.Id,
                completedNewest.Id,
                completedSameTimeHigherId.Id,
                trPlanned.Id,
                ukPlanned.Id,
                inProgress.Id,
                cancelled.Id);
        });
    }

    private async Task<JsonDocument> GetListAsync(string query = "")
    {
        using var response = await Client.GetAsync($"/api/visits{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static Visit CreateVisit(long employeeId, long storeId, DateOnly plannedDate)
    {
        return new Visit(
            employeeId,
            storeId,
            plannedDate,
            new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
    }

    private static Visit CreateCompletedVisit(
        long employeeId,
        long storeId,
        DateOnly plannedDate,
        DateTime completedAt)
    {
        var visit = CreateVisit(employeeId, storeId, plannedDate);
        visit.Start(completedAt.AddHours(-1), 39.9335, 32.8598);
        visit.Complete(completedAt, "Completed for list test.");
        return visit;
    }

    private static long[] GetIds(JsonElement root)
    {
        return root.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToArray();
    }

    private static DateOnly[] GetPlannedDates(JsonElement root)
    {
        return root.GetProperty("items")
            .EnumerateArray()
            .Select(item => DateOnly.Parse(item.GetProperty("plannedDate").GetString()!))
            .ToArray();
    }

    private sealed class SeededVisits
    {
        public SeededVisits(
            long employeeAId,
            long deStoreId,
            long completedOldestId,
            long completedNewestId,
            long completedSameTimeHigherId,
            long trPlannedId,
            long ukPlannedId,
            long inProgressId,
            long cancelledId)
        {
            EmployeeAId = employeeAId;
            DeStoreId = deStoreId;
            CompletedOldestId = completedOldestId;
            CompletedNewestId = completedNewestId;
            CompletedSameTimeHigherId = completedSameTimeHigherId;
            TrPlannedId = trPlannedId;
            UkPlannedId = ukPlannedId;
            InProgressId = inProgressId;
            CancelledId = cancelledId;
        }

        public long EmployeeAId { get; }

        public long DeStoreId { get; }

        public long CompletedOldestId { get; }

        public long CompletedNewestId { get; }

        public long CompletedSameTimeHigherId { get; }

        public long TrPlannedId { get; }

        public long UkPlannedId { get; }

        public long InProgressId { get; }

        public long CancelledId { get; }

        public long[] MixedOrder =>
        [
            CompletedSameTimeHigherId,
            CompletedNewestId,
            CompletedOldestId,
            TrPlannedId,
            InProgressId,
            UkPlannedId,
            CancelledId
        ];
    }
}
