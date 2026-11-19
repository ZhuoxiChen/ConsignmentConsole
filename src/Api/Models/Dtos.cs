namespace Api.Models;

public sealed record ConsignmentDto(Guid Id, string ConsignmentNumber, string CustomerName, string ReceiverName, string ReceiverSuburb, string ReceiverState, string ReceiverPostcode, int ItemCount, decimal WeightKg, string ServiceType, string Status, DateTimeOffset CreatedAt, int Version);

public sealed record StatusHistoryDto(string FromStatus, string ToStatus, DateTimeOffset ChangedAt, string? SignedBy, DateTimeOffset? DeliveryTime, string? FailureReason);

public sealed record ConsignmentDetailDto(Guid Id, string ConsignmentNumber, string CustomerName, string ReceiverName, string ReceiverSuburb, string ReceiverState, string ReceiverPostcode, int ItemCount, decimal WeightKg, string ServiceType, string Status, DateTimeOffset CreatedAt, int Version, IReadOnlyList<StatusHistoryDto> History);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record UpdateStatusDto(string Status, int Version, string? SignedBy, DateTimeOffset? DeliveryTime, string? FailureReason);
