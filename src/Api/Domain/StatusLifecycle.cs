namespace Api.Domain;

public static class StatusLifecycle
{
    private static readonly HashSet<string> ValidStatuses =
        ["booked", "picked_up", "in_transit", "out_for_delivery", "delivered", "failed_delivery", "returned", "cancelled"];

    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
    {
        ["booked"] = ["picked_up", "cancelled"],
        ["picked_up"] = ["in_transit"],
        ["in_transit"] = ["out_for_delivery"],
        ["out_for_delivery"] = ["delivered", "failed_delivery"],
        ["failed_delivery"] = ["out_for_delivery", "returned"]
    };

    public static bool IsKnownStatus(string status) => ValidStatuses.Contains(status);

    public static bool IsAllowed(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static IReadOnlySet<string> FailureReasons { get; } =
        new HashSet<string>(["no_one_home", "address_incorrect", "refused", "access_issue"]);
}
