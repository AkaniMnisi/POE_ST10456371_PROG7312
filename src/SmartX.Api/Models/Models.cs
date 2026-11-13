namespace SmartX.Models;

// Generic packets preserve the original Part 1 design; this gateway specialises T as double.
public record TelemetryPacket<T>(string SensorId, T Payload, DateTimeOffset? Timestamp = null);
public record Reading(long Sequence, string SensorId, double Value, DateTimeOffset Timestamp, bool Critical);
public record Device(string Id, string Mac, string Name, string Category, string Location,
    bool Online = true, bool Enabled = false, double? Value = null, DateTimeOffset? LastSeen = null);
public record Registration(string Id, string Mac, string Name, string Category, string Location);
public record Link(string From, string To, double Weight);
public record OverrideRequest(string SensorId, bool Enabled);
public record OnlineRequest(bool Online);
public record ProcessingRequest(bool Paused);
public record SearchRequest(string Query);
public record Command(long Id, string SensorId, bool Before, bool After, DateTimeOffset Timestamp);
public record AuditEntry(DateTimeOffset Timestamp, string Kind, string Message);
public record Attachment(string Id, string SensorId, string Name, string ContentType, string Base64, DateTimeOffset Uploaded);
public record Insight(string SensorId, string Title, string Reason, string? TargetId, bool? Enabled, int EvidenceCount);
public record HistoryResult(Reading[] Readings, int TotalMatches, int VisitedNodes, int TreeHeight, int TreeCount);
public record MeshResult(Device[] Nodes, Link[] Links, Link[] Forest, string[] Reachable,
    string[] Unreachable, string[] Route, double? RouteCost, double ForestCost, int Components);
// This immutable snapshot is persisted only after processing, then rebuilt into live indexes on load.
public record PersistedState(Device[] Devices, Reading[] Readings, Link[] Links, Command[] UndoBottomFirst,
    AuditEntry[] Audit, string[] Alerts, Dictionary<string, int> Patterns, Attachment[] Attachments, long Sequence);

// Part 1 operator overloading: aggregation and comparisons have explicit, testable semantics.
public record SensorData(string SensorId, double Value)
{
    public static SensorData operator +(SensorData a, SensorData b) => new($"{a.SensorId}+{b.SensorId}", a.Value + b.Value);
    public static bool operator >(SensorData a, SensorData b) => a.Value > b.Value;
    public static bool operator <(SensorData a, SensorData b) => a.Value < b.Value;
}
public record LocationNode(string Name, params LocationNode[] Children)
{
    // Recursive traversal fixes the original dictionary lookup that stopped at Zone 1.
    public bool Contains(string target) => Name == target || Children.Any(child => child.Contains(target));
}
