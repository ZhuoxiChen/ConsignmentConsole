using System.Globalization;
using System.Text.RegularExpressions;
using Api.Domain;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Data;

public sealed record CsvImportReport(
    int TotalRowsProcessed,
    int Imported,
    int Skipped,
    int Rejected,
    IReadOnlyList<CsvImportIssue> Issues);

public sealed record CsvImportIssue(int Row, string? TenantCode, string? ConsignmentNumber, string Reason);

public sealed class CsvImportFileException(string message, Exception innerException)
    : Exception(message, innerException)
{
}

public sealed class LatestCsvImportReport
{
    public CsvImportReport? Report { get; private set; }

    public void Set(CsvImportReport report) => Report = report;
}

public sealed class CsvImportService(ConsignmentDbContext db)
{
    private const string ConsignmentUniqueConstraint = "ux_consignments_tenant_number";
    private const string TenantUniqueConstraint = "ux_tenants_code";
    private const decimal MaximumWeightKg = 99999999.99m;
    private static readonly Regex IsoTimestampPattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant);
    private readonly ConsignmentDbContext _db = db;

    public async Task<CsvImportReport> ImportAsync(string csvPath, CancellationToken cancellationToken = default)
    {
        var issues = new List<CsvImportIssue>();
        var totalRowsProcessed = 0;
        var imported = 0;
        var skipped = 0;
        var rejected = 0;

        await using var stream = File.OpenRead(csvPath);
        using var reader = new StreamReader(stream);
        var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null
        };
        using var parser = new CsvHelper.CsvReader(reader, csvConfiguration);

        try
        {
            await foreach (var row in parser.GetRecordsAsync<CsvRow>(cancellationToken))
            {
                totalRowsProcessed++;
                var rowNumber = totalRowsProcessed + 1;
                var tenantCode = NormalizeTenantCode(row.tenant_code);
                var consignmentNumber = Normalize(row.consignment_number);

                if (!TryValidate(row, out var validatedRow, out var reason))
                {
                    rejected++;
                    issues.Add(new CsvImportIssue(
                        rowNumber,
                        tenantCode.Length == 0 ? null : tenantCode,
                        consignmentNumber.Length == 0 ? null : consignmentNumber,
                        reason));
                    continue;
                }

                Tenant tenant;
                try
                {
                    tenant = await ResolveTenantAsync(validatedRow.TenantCode, cancellationToken);
                }
                catch (CsvImportPersistenceException exception)
                {
                    rejected++;
                    issues.Add(new CsvImportIssue(
                        rowNumber,
                        validatedRow.TenantCode,
                        validatedRow.ConsignmentNumber,
                        exception.Message));
                    continue;
                }

                var existing = await _db.Consignments
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.TenantId == tenant.Id && x.ConsignmentNumber == validatedRow.ConsignmentNumber,
                        cancellationToken);

                if (existing is not null)
                {
                    RecordExisting(existing, validatedRow, rowNumber, issues, ref skipped, ref rejected);
                    continue;
                }

                var consignment = new Consignment
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ConsignmentNumber = validatedRow.ConsignmentNumber,
                    CustomerName = validatedRow.CustomerName,
                    ReceiverName = validatedRow.ReceiverName,
                    ReceiverSuburb = validatedRow.ReceiverSuburb,
                    ReceiverState = validatedRow.ReceiverState,
                    ReceiverPostcode = validatedRow.ReceiverPostcode,
                    ItemCount = validatedRow.ItemCount,
                    WeightKg = validatedRow.WeightKg,
                    ServiceType = validatedRow.ServiceType,
                    Status = validatedRow.Status,
                    CreatedAt = validatedRow.CreatedAt
                };

                _db.Consignments.Add(consignment);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    imported++;
                }
                catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, ConsignmentUniqueConstraint))
                {
                    _db.Entry(consignment).State = EntityState.Detached;
                    var racedExisting = await _db.Consignments
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            x => x.TenantId == tenant.Id && x.ConsignmentNumber == validatedRow.ConsignmentNumber,
                            cancellationToken);

                    if (racedExisting is null)
                    {
                        throw new InvalidOperationException(
                            $"Consignment '{validatedRow.ConsignmentNumber}' violated the tenant-scoped unique constraint, but no matching row could be reloaded.",
                            exception);
                    }

                    RecordExisting(racedExisting, validatedRow, rowNumber, issues, ref skipped, ref rejected);
                }
                catch (DbUpdateException exception) when (IsDataConstraintViolation(exception))
                {
                    _db.Entry(consignment).State = EntityState.Detached;
                    rejected++;
                    issues.Add(new CsvImportIssue(
                        rowNumber,
                        validatedRow.TenantCode,
                        validatedRow.ConsignmentNumber,
                        "Database rejected the row because it violates a data constraint"));
                }
            }
        }
        catch (CsvHelperException exception)
        {
            throw new CsvImportFileException(
                $"CSV import failed at the file level because the header is invalid or CSV syntax is malformed: {exception.Message}",
                exception);
        }

        return new CsvImportReport(totalRowsProcessed, imported, skipped, rejected, issues);
    }

    private async Task<Tenant> ResolveTenantAsync(string tenantCode, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.SingleOrDefaultAsync(
            x => x.Code.ToLower() == tenantCode,
            cancellationToken);
        if (tenant is not null)
        {
            return tenant;
        }

        tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = tenantCode,
            Name = tenantCode.Replace('-', ' ')
        };
        _db.Tenants.Add(tenant);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return tenant;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, TenantUniqueConstraint))
        {
            _db.Entry(tenant).State = EntityState.Detached;
            var racedTenant = await _db.Tenants.SingleOrDefaultAsync(
                x => x.Code.ToLower() == tenantCode,
                cancellationToken);
            if (racedTenant is null)
            {
                throw new InvalidOperationException(
                    $"Tenant '{tenantCode}' violated the tenant-code unique constraint, but no matching tenant could be reloaded.",
                    exception);
            }

            return racedTenant;
        }
        catch (DbUpdateException exception) when (IsDataConstraintViolation(exception))
        {
            _db.Entry(tenant).State = EntityState.Detached;
            throw new CsvImportPersistenceException(
                "Database rejected the row because its tenant code violates a data constraint",
                exception);
        }
    }

    private static void RecordExisting(
        Consignment existing,
        ValidatedCsvRow incoming,
        int rowNumber,
        List<CsvImportIssue> issues,
        ref int skipped,
        ref int rejected)
    {
        if (HasSameCanonicalData(existing, incoming))
        {
            skipped++;
            issues.Add(new CsvImportIssue(
                rowNumber,
                incoming.TenantCode,
                incoming.ConsignmentNumber,
                "Exact duplicate for tenant/consignment number; existing row unchanged"));
            return;
        }

        rejected++;
        issues.Add(new CsvImportIssue(
            rowNumber,
            incoming.TenantCode,
            incoming.ConsignmentNumber,
            "Conflicting duplicate for tenant/consignment number; existing row unchanged"));
    }

    private static bool HasSameCanonicalData(Consignment existing, ValidatedCsvRow incoming) =>
        Normalize(existing.ConsignmentNumber) == incoming.ConsignmentNumber &&
        Normalize(existing.CustomerName) == incoming.CustomerName &&
        Normalize(existing.ReceiverName) == incoming.ReceiverName &&
        Normalize(existing.ReceiverSuburb) == incoming.ReceiverSuburb &&
        Normalize(existing.ReceiverState) == incoming.ReceiverState &&
        Normalize(existing.ReceiverPostcode) == incoming.ReceiverPostcode &&
        existing.ItemCount == incoming.ItemCount &&
        existing.WeightKg == incoming.WeightKg &&
        Normalize(existing.ServiceType) == incoming.ServiceType &&
        NormalizeStatus(existing.Status) == incoming.Status &&
        existing.CreatedAt == incoming.CreatedAt;

    private static bool TryValidate(CsvRow row, out ValidatedCsvRow validated, out string reason)
    {
        var tenantCode = NormalizeTenantCode(row.tenant_code);
        var consignmentNumber = Normalize(row.consignment_number);
        var customerName = Normalize(row.customer_name);
        var receiverName = Normalize(row.receiver_name);
        var receiverSuburb = Normalize(row.receiver_suburb);
        var receiverState = Normalize(row.receiver_state);
        var receiverPostcode = Normalize(row.receiver_postcode);
        var serviceType = Normalize(row.service_type);
        var status = NormalizeStatus(row.status);

        if (tenantCode.Length == 0) return Invalid("tenant_code is required", out validated, out reason);
        if (tenantCode.Length > 100) return Invalid("tenant_code must be at most 100 characters", out validated, out reason);
        if (consignmentNumber.Length == 0) return Invalid("consignment_number is required", out validated, out reason);
        if (consignmentNumber.Length > 100) return Invalid("consignment_number must be at most 100 characters", out validated, out reason);
        if (customerName.Length == 0) return Invalid("customer_name is required", out validated, out reason);
        if (customerName.Length > 200) return Invalid("customer_name must be at most 200 characters", out validated, out reason);
        if (receiverName.Length == 0) return Invalid("receiver_name is required", out validated, out reason);
        if (receiverName.Length > 200) return Invalid("receiver_name must be at most 200 characters", out validated, out reason);
        if (receiverSuburb.Length == 0) return Invalid("receiver_suburb is required", out validated, out reason);
        if (receiverSuburb.Length > 200) return Invalid("receiver_suburb must be at most 200 characters", out validated, out reason);
        if (receiverState.Length == 0) return Invalid("receiver_state is required", out validated, out reason);
        if (receiverState.Length > 100) return Invalid("receiver_state must be at most 100 characters", out validated, out reason);
        if (receiverPostcode.Length == 0) return Invalid("receiver_postcode is required", out validated, out reason);
        if (receiverPostcode.Length > 20) return Invalid("receiver_postcode must be at most 20 characters", out validated, out reason);
        if (!int.TryParse(row.item_count?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemCount) || itemCount < 0)
        {
            return Invalid("item_count must be a non-negative integer", out validated, out reason);
        }

        if (!decimal.TryParse(
                row.weight_kg?.Trim(),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var weightKg) ||
            weightKg < 0)
        {
            return Invalid("weight_kg must be a non-negative number", out validated, out reason);
        }

        if (weightKg > MaximumWeightKg)
        {
            return Invalid("weight_kg exceeds the supported maximum of 99999999.99", out validated, out reason);
        }

        if (decimal.Round(weightKg, 2) != weightKg)
        {
            return Invalid("weight_kg must have at most 2 decimal places", out validated, out reason);
        }

        if (serviceType.Length == 0) return Invalid("service_type is required", out validated, out reason);
        if (serviceType.Length > 100) return Invalid("service_type must be at most 100 characters", out validated, out reason);
        if (status.Length == 0 || !StatusLifecycle.IsKnownStatus(status))
        {
            return Invalid("status must be a known lifecycle status", out validated, out reason);
        }

        if (status.Length > 30) return Invalid("status must be at most 30 characters", out validated, out reason);
        if (!TryParseCreatedAt(row.created_at, out var createdAt))
        {
            return Invalid("created_at must be an ISO 8601 timestamp with an offset or dd/MM/yyyy H:mm", out validated, out reason);
        }

        validated = new ValidatedCsvRow(
            tenantCode,
            consignmentNumber,
            customerName,
            receiverName,
            receiverSuburb,
            receiverState,
            receiverPostcode,
            itemCount,
            weightKg,
            serviceType,
            status,
            createdAt);
        reason = "";
        return true;
    }

    private static bool Invalid(string message, out ValidatedCsvRow validated, out string reason)
    {
        validated = null!;
        reason = message;
        return false;
    }

    private static bool TryParseCreatedAt(string? value, out DateTimeOffset result)
    {
        var timestamp = value?.Trim();
        if (timestamp is not null &&
            DateTime.TryParseExact(
                timestamp,
                "dd/MM/yyyy H:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var utcDateTime))
        {
            result = new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
            return true;
        }

        if (timestamp is not null &&
            IsoTimestampPattern.IsMatch(timestamp) &&
            DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = parsed.ToUniversalTime();
            return true;
        }

        result = default;
        return false;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName) =>
        GetPostgresException(exception) is
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException &&
        postgresException.ConstraintName == constraintName;

    private static bool IsDataConstraintViolation(DbUpdateException exception)
    {
        var sqlState = GetPostgresException(exception)?.SqlState;
        return sqlState is not null &&
            (sqlState.StartsWith("22", StringComparison.Ordinal) ||
             sqlState.StartsWith("23", StringComparison.Ordinal));
    }

    private static PostgresException? GetPostgresException(DbUpdateException exception) =>
        exception.GetBaseException() as PostgresException;

    private static string Normalize(string? value) => value?.Trim() ?? "";

    private static string NormalizeStatus(string? value) => Normalize(value).ToLowerInvariant();

    private static string NormalizeTenantCode(string? value) => Normalize(value).ToLowerInvariant();

    private sealed record ValidatedCsvRow(
        string TenantCode,
        string ConsignmentNumber,
        string CustomerName,
        string ReceiverName,
        string ReceiverSuburb,
        string ReceiverState,
        string ReceiverPostcode,
        int ItemCount,
        decimal WeightKg,
        string ServiceType,
        string Status,
        DateTimeOffset CreatedAt);

    private sealed class CsvImportPersistenceException(string message, Exception innerException)
        : Exception(message, innerException)
    {
    }

    private sealed class CsvRow
    {
        public string? tenant_code { get; set; }
        public string? consignment_number { get; set; }
        public string? customer_name { get; set; }
        public string? receiver_name { get; set; }
        public string? receiver_suburb { get; set; }
        public string? receiver_state { get; set; }
        public string? receiver_postcode { get; set; }
        public string? item_count { get; set; }
        public string? weight_kg { get; set; }
        public string? service_type { get; set; }
        public string? status { get; set; }
        public string? created_at { get; set; }
    }
}
