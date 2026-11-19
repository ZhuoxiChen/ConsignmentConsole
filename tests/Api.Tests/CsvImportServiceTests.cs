using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class CsvImportServiceTests
{
    [Fact]
    public async Task Imports_valid_rows_and_preserves_iso_offset_timestamp()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row());

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        var consignment = await db.Consignments.SingleAsync();
        Assert.Equal(1, report.TotalRowsProcessed);
        Assert.Equal(1, report.Imported);
        Assert.Equal(0, report.Skipped);
        Assert.Equal(0, report.Rejected);
        Assert.Equal("swiftline-freight", (await db.Tenants.SingleAsync()).Code);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 22, 30, 0, TimeSpan.Zero), consignment.CreatedAt);
    }

    [Fact]
    public async Task Rejects_negative_weight_and_continues_with_later_rows()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(
            Row(number: "CN-NEGATIVE", weight: "-3.5"),
            Row(number: "CN-VALID"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(2, report.TotalRowsProcessed);
        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Rejected);
        Assert.Contains("weight_kg", Assert.Single(report.Issues).Reason);
        Assert.Equal("CN-VALID", (await db.Consignments.SingleAsync()).ConsignmentNumber);
    }

    [Fact]
    public async Task Rejects_missing_postcode()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-NO-POSTCODE", postcode: ""));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Rejected);
        Assert.Contains("receiver_postcode", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Rejects_short_row_and_continues_with_later_rows()
    {
        await using var db = CreateContext();
        var shortRow = string.Join(",", Row(number: "CN-SHORT").Split(',').Take(11));
        using var csv = new TempCsvFile(shortRow, Row(number: "CN-AFTER-SHORT"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(2, report.TotalRowsProcessed);
        Assert.Equal(1, report.Rejected);
        Assert.Equal(1, report.Imported);
        Assert.Contains("created_at", Assert.Single(report.Issues).Reason);
        Assert.Equal("CN-AFTER-SHORT", (await db.Consignments.SingleAsync()).ConsignmentNumber);
    }

    [Fact]
    public async Task Invalid_header_fails_the_import_at_file_level()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-INVALID-HEADER"));
        File.WriteAllText(csv.Path, "invalid,header" + Environment.NewLine + Row(number: "CN-INVALID-HEADER"));

        var exception = await Assert.ThrowsAsync<CsvImportFileException>(
            () => new CsvImportService(db).ImportAsync(csv.Path));

        Assert.Contains("file level", exception.Message);
        Assert.Empty(await db.Consignments.ToListAsync());
    }

    [Fact]
    public async Task Malformed_quoted_field_fails_instead_of_returning_an_import_report()
    {
        await using var db = CreateContext();
        var malformedRow = Row(number: "CN-MALFORMED").Replace("Test Customer", "\"Unclosed Customer");
        using var csv = new TempCsvFile(Row(number: "CN-BEFORE-MALFORMED"), malformedRow);

        var exception = await Assert.ThrowsAsync<CsvImportFileException>(
            () => new CsvImportService(db).ImportAsync(csv.Path));

        Assert.Contains("file level", exception.Message);
        Assert.DoesNotContain("CN-MALFORMED", (await db.Consignments.ToListAsync()).Select(x => x.ConsignmentNumber));
    }

    [Fact]
    public async Task Rejects_unknown_status()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-UNKNOWN-STATUS", status: "DELIVRD"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Rejected);
        Assert.Contains("status", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Parses_supported_legacy_timestamp_as_utc()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(
            Row(number: "CN-ISO", createdAt: "2026-11-17T09:30:00+11:00"),
            Row(number: "CN-LEGACY", createdAt: "17/11/2026 9:30"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        var consignments = await db.Consignments.ToDictionaryAsync(x => x.ConsignmentNumber);
        Assert.Equal(2, report.Imported);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 22, 30, 0, TimeSpan.Zero), consignments["CN-ISO"].CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 9, 30, 0, TimeSpan.Zero), consignments["CN-LEGACY"].CreatedAt);
    }

    [Fact]
    public async Task Parses_negative_offset_timestamp_as_utc()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-NEGATIVE-OFFSET", createdAt: "2026-11-17T09:30:00-05:30"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Imported);
        Assert.Equal(
            new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero),
            (await db.Consignments.SingleAsync()).CreatedAt);
    }

    [Fact]
    public async Task Parses_fractional_seconds_in_iso_timestamp()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-FRACTIONAL-SECONDS", createdAt: "2026-11-17T09:30:00.1234567+11:00"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Imported);
        Assert.Equal(
            new DateTimeOffset(2026, 11, 16, 22, 30, 0, TimeSpan.Zero).AddTicks(1234567),
            (await db.Consignments.SingleAsync()).CreatedAt);
    }

    [Fact]
    public async Task Rejects_offsetless_iso_timestamp()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-OFFSETLESS", createdAt: "2026-11-17T09:30:00"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Rejected);
        Assert.Contains("created_at", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Rejects_unsupported_timestamp_format()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-BAD-DATE", createdAt: "11/17/2026 9:30"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Rejected);
        Assert.Contains("created_at", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Skips_exact_duplicate_without_creating_another_record()
    {
        await using var db = CreateContext();
        var row = Row(number: "CN-DUPLICATE");
        using var csv = new TempCsvFile(row, row);

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(0, report.Rejected);
        Assert.Equal(1, await db.Consignments.CountAsync());
        Assert.Contains("Exact duplicate", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Uses_canonical_values_for_duplicate_comparison()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(
            Row(number: "CN-CANONICAL", status: "booked", createdAt: "2026-11-17T09:30:00+11:00"),
            Row(
                tenant: " SWIFTLINE-FREIGHT ",
                number: " CN-CANONICAL ",
                customer: " Test Customer ",
                receiver: " Pat Taylor ",
                suburb: " Sydney ",
                state: " NSW ",
                postcode: " 2000 ",
                itemCount: "2",
                weight: "4.5",
                service: " standard ",
                status: " BOOKED ",
                createdAt: "2026-11-16T22:30:00Z"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(1, await db.Tenants.CountAsync());
        Assert.Equal(1, await db.Consignments.CountAsync());
    }

    [Fact]
    public async Task Rejects_conflicting_duplicate_without_overwriting_first_record()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(
            Row(number: "CN-CONFLICT", receiver: "Original Receiver"),
            Row(number: "CN-CONFLICT", receiver: "Changed Receiver"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        var existing = await db.Consignments.SingleAsync();
        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Rejected);
        Assert.Equal("Original Receiver", existing.ReceiverName);
        Assert.Contains("Conflicting duplicate", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Reimport_does_not_overwrite_existing_record_changed_after_import()
    {
        await using var db = CreateContext();
        var tenant = new Tenant { Id = Guid.NewGuid(), Code = "swiftline-freight", Name = "Swiftline Freight" };
        var existing = new Consignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Tenant = tenant,
            ConsignmentNumber = "CN-API-UPDATED",
            CustomerName = "Test Customer",
            ReceiverName = "Pat Taylor",
            ReceiverSuburb = "Sydney",
            ReceiverState = "NSW",
            ReceiverPostcode = "2000",
            ItemCount = 2,
            WeightKg = 4.50m,
            ServiceType = "standard",
            Status = "picked_up",
            CreatedAt = new DateTimeOffset(2026, 11, 16, 22, 30, 0, TimeSpan.Zero)
        };
        db.Consignments.Add(existing);
        await db.SaveChangesAsync();
        using var csv = new TempCsvFile(Row(number: "CN-API-UPDATED", status: "booked"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(1, report.Rejected);
        Assert.Equal("picked_up", (await db.Consignments.SingleAsync()).Status);
        Assert.Contains("Conflicting duplicate", Assert.Single(report.Issues).Reason);
    }

    [Fact]
    public async Task Allows_same_consignment_number_under_different_tenants()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(
            Row(tenant: "swiftline-freight", number: "CN-SHARED"),
            Row(tenant: "coastal-couriers", number: "CN-SHARED"));

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(2, report.Imported);
        Assert.Equal(0, report.Skipped);
        Assert.Equal(0, report.Rejected);
        Assert.Equal(2, await db.Tenants.CountAsync());
        Assert.Equal(2, await db.Consignments.CountAsync());
    }

    [Fact]
    public async Task Rerunning_import_is_idempotent()
    {
        await using var db = CreateContext();
        using var csv = new TempCsvFile(Row(number: "CN-IDEMPOTENT"));
        var importer = new CsvImportService(db);

        var firstReport = await importer.ImportAsync(csv.Path);
        var secondReport = await importer.ImportAsync(csv.Path);

        Assert.Equal(1, firstReport.Imported);
        Assert.Equal(0, secondReport.Imported);
        Assert.Equal(1, secondReport.Skipped);
        Assert.Equal(0, secondReport.Rejected);
        Assert.Equal(1, await db.Consignments.CountAsync());
    }

    [Fact]
    public async Task Report_contains_counts_row_numbers_and_actionable_reasons()
    {
        await using var db = CreateContext();
        var validRow = Row(number: "CN-REPORT");
        using var csv = new TempCsvFile(
            validRow,
            Row(number: "CN-INVALID", status: "DELIVRD"),
            validRow);

        var report = await new CsvImportService(db).ImportAsync(csv.Path);

        Assert.Equal(3, report.TotalRowsProcessed);
        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(1, report.Rejected);
        Assert.Equal(2, report.Issues.Count);
        Assert.Equal(3, report.Issues[0].Row);
        Assert.Contains("status", report.Issues[0].Reason);
        Assert.Equal(4, report.Issues[1].Row);
        Assert.Contains("Exact duplicate", report.Issues[1].Reason);
    }

    [Fact]
    public async Task Supplied_csv_quality_cases_follow_import_rules()
    {
        await using var db = CreateContext();
        var csvPath = Path.Combine(AppContext.BaseDirectory, "consignments.csv");

        var report = await new CsvImportService(db).ImportAsync(csvPath);

        Assert.Equal(68, report.TotalRowsProcessed);
        Assert.Equal(63, report.Imported);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(4, report.Rejected);
        Assert.Contains("weight_kg", Assert.Single(report.Issues.Where(issue => issue.ConsignmentNumber == "CN100032")).Reason);
        Assert.Contains("receiver_postcode", Assert.Single(report.Issues.Where(issue => issue.ConsignmentNumber == "CN200031")).Reason);
        Assert.Contains("status", Assert.Single(report.Issues.Where(issue => issue.ConsignmentNumber == "CN100031")).Reason);
        Assert.Contains("Exact duplicate", Assert.Single(report.Issues.Where(issue => issue.ConsignmentNumber == "CN100008")).Reason);
        Assert.Contains("Conflicting duplicate", Assert.Single(report.Issues.Where(issue => issue.ConsignmentNumber == "CN200011")).Reason);

        var sharedNumberRows = await db.Consignments
            .Where(consignment => consignment.ConsignmentNumber == "CN100017")
            .ToListAsync();
        Assert.Equal(2, sharedNumberRows.Count);
        Assert.Equal(2, sharedNumberRows.Select(consignment => consignment.TenantId).Distinct().Count());
        Assert.Equal(
            new DateTimeOffset(2026, 11, 17, 9, 30, 0, TimeSpan.Zero),
            (await db.Consignments.SingleAsync(consignment => consignment.ConsignmentNumber == "CN200032")).CreatedAt);
    }

    private static ConsignmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConsignmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ConsignmentDbContext(options);
    }

    private static string Row(
        string tenant = "swiftline-freight",
        string number = "CN-1",
        string customer = "Test Customer",
        string receiver = "Pat Taylor",
        string suburb = "Sydney",
        string state = "NSW",
        string postcode = "2000",
        string itemCount = "2",
        string weight = "4.50",
        string service = "standard",
        string status = "booked",
        string createdAt = "2026-11-17T09:30:00+11:00") =>
        string.Join(",", tenant, number, customer, receiver, suburb, state, postcode, itemCount, weight, service, status, createdAt);

    private sealed class TempCsvFile : IDisposable
    {
        public TempCsvFile(params string[] rows)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"consignment-import-{Guid.NewGuid():N}.csv");
            File.WriteAllText(
                Path,
                "tenant_code,consignment_number,customer_name,receiver_name,receiver_suburb,receiver_state,receiver_postcode,item_count,weight_kg,service_type,status,created_at" +
                Environment.NewLine +
                string.Join(Environment.NewLine, rows));
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}