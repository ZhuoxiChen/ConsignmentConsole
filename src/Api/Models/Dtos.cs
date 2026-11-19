using Api.Domain;

namespace Api.Models;

public sealed record ConsignmentDto(Guid Id, string ConsignmentNumber, string CustomerName, string ReceiverName, string ReceiverSuburb, string ReceiverState, string ReceiverPostcode, int ItemCount, decimal WeightKg, string ServiceType, string Status, DateTimeOffset CreatedAt, int Version);

public sealed record StatusHistoryDto(Guid Id, string FromStatus, string ToStatus, DateTimeOffset ChangedAt, string? SignedBy, DateTimeOffset? DeliveryTime, string? FailureReason);

public sealed record ConsignmentDetailDto(Guid Id, string ConsignmentNumber, string CustomerName, string ReceiverName, string ReceiverSuburb, string ReceiverState, string ReceiverPostcode, int ItemCount, decimal WeightKg, string ServiceType, string Status, DateTimeOffset CreatedAt, int Version, IReadOnlyList<StatusHistoryDto> History);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record UpdateStatusDto(string Status, int Version, string? SignedBy, DateTimeOffset? DeliveryTime, string? FailureReason);

public static class ConsignmentDtoMapper
{
    public static ConsignmentDto ToDto(Consignment consignment) =>
        new(
            consignment.Id,
            consignment.ConsignmentNumber,
            consignment.CustomerName,
            consignment.ReceiverName,
            consignment.ReceiverSuburb,
            consignment.ReceiverState,
            consignment.ReceiverPostcode,
            consignment.ItemCount,
            consignment.WeightKg,
            consignment.ServiceType,
            consignment.Status,
            consignment.CreatedAt,
            consignment.Version);

    public static ConsignmentDetailDto ToDetailDto(Consignment consignment) =>
        new(
            consignment.Id,
            consignment.ConsignmentNumber,
            consignment.CustomerName,
            consignment.ReceiverName,
            consignment.ReceiverSuburb,
            consignment.ReceiverState,
            consignment.ReceiverPostcode,
            consignment.ItemCount,
            consignment.WeightKg,
            consignment.ServiceType,
            consignment.Status,
            consignment.CreatedAt,
            consignment.Version,
            consignment.History
                .OrderBy(history => history.ChangedAt)
                .Select(history => new StatusHistoryDto(
                    history.Id,
                    history.FromStatus,
                    history.ToStatus,
                    history.ChangedAt,
                    history.SignedBy,
                    history.DeliveryTime,
                    history.FailureReason))
                .ToArray());
}
