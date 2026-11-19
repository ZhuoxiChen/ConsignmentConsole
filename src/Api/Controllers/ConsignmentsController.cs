using Api.Domain;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/consignments")]
public sealed class ConsignmentsController(ConsignmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConsignmentDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await service.GetTenantIdAsync(GetTenantHeader(), cancellationToken);
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            var errors = new Dictionary<string, string[]>();
            if (page < 1)
            {
                errors["page"] = ["Page must be a positive integer."];
            }

            if (pageSize < 1 || pageSize > 100)
            {
                errors["pageSize"] = ["Page size must be between 1 and 100."];
            }

            throw new ValidationException(new ApiError(
                "VALIDATION",
                "Paging values are invalid.",
                errors));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (!StatusLifecycle.IsKnownStatus(normalizedStatus))
            {
                throw new ValidationException(new ApiError(
                    "VALIDATION",
                    "Status filter is invalid.",
                    new Dictionary<string, string[]> { ["status"] = ["Choose a valid lifecycle status."] }));
            }

            status = normalizedStatus;
        }

        var (items, total) = await service.ListAsync(
            tenantId,
            status,
            search,
            page,
            pageSize,
            cancellationToken);
        return Ok(new PagedResult<ConsignmentDto>(
            items.Select(ConsignmentDtoMapper.ToDto).ToArray(),
            total,
            page,
            pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConsignmentDetailDto>> Get(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await service.GetTenantIdAsync(GetTenantHeader(), cancellationToken);
        var consignment = await service.GetAsync(tenantId, id, cancellationToken);
        return Ok(ConsignmentDtoMapper.ToDetailDto(consignment));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ConsignmentDetailDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await service.GetTenantIdAsync(GetTenantHeader(), cancellationToken);
        var consignment = await service.UpdateAsync(
            tenantId,
            id,
            new StatusUpdateRequest(
                request.Status,
                request.Version,
                request.SignedBy,
                request.DeliveryTime,
                request.FailureReason),
            cancellationToken);
        return Ok(ConsignmentDtoMapper.ToDetailDto(consignment));
    }

    private string? GetTenantHeader() => Request.Headers["X-Tenant"].FirstOrDefault();
}
