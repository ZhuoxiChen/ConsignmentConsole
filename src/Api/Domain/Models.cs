namespace Api.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public List<Consignment> Consignments { get; set; } = [];
}

public sealed class Consignment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string ConsignmentNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ReceiverName { get; set; } = "";
    public string ReceiverSuburb { get; set; } = "";
    public string ReceiverState { get; set; } = "";
    public string ReceiverPostcode { get; set; } = "";
    public int ItemCount { get; set; }
    public decimal WeightKg { get; set; }
    public string ServiceType { get; set; } = "";
    public string Status { get; set; } = "booked";
    public DateTimeOffset CreatedAt { get; set; }
    public int Version { get; set; } = 1;
    public List<StatusHistory> History { get; set; } = [];
}

public sealed class StatusHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConsignmentId { get; set; }
    public Consignment Consignment { get; set; } = null!;
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
    public string? SignedBy { get; set; }
    public DateTimeOffset? DeliveryTime { get; set; }
    public string? FailureReason { get; set; }
}
