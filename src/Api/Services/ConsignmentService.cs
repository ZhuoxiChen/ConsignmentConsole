using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors = null);
public sealed class NotFoundException : Exception;
public sealed class ConflictException(ApiError Error, Consignment Latest) : Exception
{
    public ApiError Error { get; } = Error;
    public Consignment Latest { get; } = Latest;
}
public sealed class ValidationException(ApiError Error) : Exception
{
    public ApiError Error { get; } = Error;
}

public sealed record StatusUpdateRequest(string Status, int Version, string? SignedBy, DateTimeOffset? DeliveryTime, string? FailureReason);

public sealed class ConsignmentService(ConsignmentDbContext db)
{
    private readonly ConsignmentDbContext _db = db;

    public async Task<Guid> GetTenantIdAsync(string? tenantCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new ValidationException(new ApiError("TENANT_REQUIRED", "The X-Tenant header is required."));
        }

        var normalizedCode = tenantCode.Trim().ToLowerInvariant();
        var tenant = await _db.Tenants.SingleOrDefaultAsync(
            x => x.Code.ToLower() == normalizedCode,
            cancellationToken);
        return tenant?.Id ?? throw new NotFoundException();
    }

    public async Task<(IReadOnlyList<Consignment> Items, int Total)> ListAsync(
        Guid tenantId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Consignments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.ConsignmentNumber.ToLower().Contains(term) || x.ReceiverName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var offset = (long)(page - 1) * pageSize;
        var items = offset > int.MaxValue
            ? []
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<Consignment> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await QueryDetail(tenantId, id)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException();
    }

    public async Task<Consignment> UpdateAsync(
        Guid tenantId,
        Guid id,
        StatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var consignment = await _db.Consignments.SingleOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException();

        if (consignment.Version != request.Version)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw await CreateConflictAsync(tenantId, id, cancellationToken);
        }

        var target = request.Status?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(target) || !StatusLifecycle.IsKnownStatus(target))
        {
            throw new ValidationException(new ApiError(
                "VALIDATION",
                "Status is invalid.",
                new Dictionary<string, string[]> { ["status"] = ["Choose a valid lifecycle status."] }));
        }

        if (!StatusLifecycle.IsAllowed(consignment.Status, target))
        {
            throw new ValidationException(new ApiError(
                "INVALID_TRANSITION",
                $"Cannot change status from '{consignment.Status}' to '{target}'."));
        }

        var errors = new Dictionary<string, string[]>();
        if (target == "delivered")
        {
            if (string.IsNullOrWhiteSpace(request.SignedBy))
            {
                errors["signedBy"] = ["Signed-by name is required for delivered consignments."];
            }

            if (!request.DeliveryTime.HasValue)
            {
                errors["deliveryTime"] = ["Delivery time is required for delivered consignments."];
            }
        }

        var failureReason = request.FailureReason?.Trim().ToLowerInvariant();
        if (target == "failed_delivery" &&
            (string.IsNullOrWhiteSpace(failureReason) || !StatusLifecycle.FailureReasons.Contains(failureReason)))
        {
            errors["failureReason"] = ["Choose no_one_home, address_incorrect, refused, or access_issue."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(new ApiError(
                "VALIDATION",
                "Proof of delivery or failure details are missing or invalid.",
                errors));
        }

        var from = consignment.Status;
        consignment.Status = target;
        consignment.Version = checked(consignment.Version + 1);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw await CreateConflictAsync(tenantId, id, cancellationToken);
        }

        var history = new StatusHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConsignmentId = consignment.Id,
            FromStatus = from,
            ToStatus = target,
            ChangedAt = DateTimeOffset.UtcNow,
            SignedBy = target == "delivered" ? request.SignedBy?.Trim() : null,
            DeliveryTime = target == "delivered" ? request.DeliveryTime : null,
            FailureReason = target == "failed_delivery" ? failureReason : null
        };
        consignment.History.Add(history);
        _db.StatusHistory.Add(history);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return consignment;
    }

    private IQueryable<Consignment> QueryDetail(Guid tenantId, Guid id) =>
        _db.Consignments
            .Where(x => x.Id == id && x.TenantId == tenantId)
            .Include(x => x.History.Where(history =>
                history.TenantId == tenantId &&
                history.ConsignmentId == id));

    private async Task<ConflictException> CreateConflictAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var latest = await QueryDetail(tenantId, id)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            throw new NotFoundException();
        }

        return new ConflictException(
            new ApiError("CONFLICT", "This consignment changed after it was opened."),
            latest);
    }
}
