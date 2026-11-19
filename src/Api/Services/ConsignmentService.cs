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

    public async Task<Guid> GetTenantIdAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.SingleOrDefaultAsync(x => x.Code == tenantCode, cancellationToken);
        return tenant?.Id ?? throw new NotFoundException();
    }

    public async Task<(IReadOnlyList<Consignment> Items, int Total)> ListAsync(string tenantCode, string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync(tenantCode, cancellationToken);
        var query = _db.Consignments.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.ConsignmentNumber.ToLower().Contains(term) || x.ReceiverName.ToLower().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<Consignment> GetAsync(string tenantCode, Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync(tenantCode, cancellationToken);
        return await _db.Consignments.Include(x => x.History).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException();
    }

    public async Task<Consignment> UpdateAsync(string tenantCode, Guid id, StatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync(tenantCode, cancellationToken);
        var consignment = await _db.Consignments.Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException();

        if (consignment.Version != request.Version)
            throw new ConflictException(new ApiError("CONFLICT", "This consignment changed after it was opened."), consignment);

        var target = request.Status?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(target) || !StatusLifecycle.IsKnownStatus(target))
            throw new ValidationException(new ApiError("VALIDATION", "Status is invalid.", new Dictionary<string, string[]> { ["status"] = ["Choose a valid lifecycle status."] }));

        if (!StatusLifecycle.IsAllowed(consignment.Status, target))
            throw new ValidationException(new ApiError("INVALID_TRANSITION", $"Cannot change status from '{consignment.Status}' to '{target}'."));

        var errors = new Dictionary<string, string[]>();
        if (target == "delivered")
        {
            if (string.IsNullOrWhiteSpace(request.SignedBy)) errors["signedBy"] = ["Signed-by name is required for delivered consignments."];
            if (!request.DeliveryTime.HasValue) errors["deliveryTime"] = ["Delivery time is required for delivered consignments."];
        }
        if (target == "failed_delivery")
        {
            if (string.IsNullOrWhiteSpace(request.FailureReason) || !StatusLifecycle.FailureReasons.Contains(request.FailureReason.Trim().ToLowerInvariant()))
                errors["failureReason"] = ["Choose no_one_home, address_incorrect, refused, or access_issue."];
        }
        if (errors.Count > 0)
            throw new ValidationException(new ApiError("VALIDATION", "Proof of delivery or failure details are missing.", errors));

        var from = consignment.Status;
        consignment.Status = target;
        consignment.History.Add(new StatusHistory
        {
            Id = Guid.NewGuid(), ConsignmentId = consignment.Id, FromStatus = from, ToStatus = target,
            ChangedAt = DateTimeOffset.UtcNow, SignedBy = target == "delivered" ? request.SignedBy?.Trim() : null,
            DeliveryTime = target == "delivered" ? request.DeliveryTime : null,
            FailureReason = target == "failed_delivery" ? request.FailureReason?.Trim().ToLowerInvariant() : null
        });

        await _db.SaveChangesAsync(cancellationToken);
        return consignment;
    }
}
