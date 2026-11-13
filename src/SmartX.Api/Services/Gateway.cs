using System.Text.Json;
using System.Text.RegularExpressions;
using SmartX.Algorithms;
using SmartX.Models;
namespace SmartX.Services;

/// <summary>Single-process gateway; all shared mutable data is protected by one short-lived lock.</summary>
public sealed class Gateway
{
    private readonly object gate = new();
    // (Microsoft, n.d.b): hash-based ID lookup avoids scanning all registered devices per packet.
    private readonly Dictionary<string, Device> devices = new(StringComparer.OrdinalIgnoreCase);
    // (Microsoft, n.d.b): FIFO Queue preserves ordinary packet arrival order.
    private readonly Queue<Reading> standard = new();
    private readonly PriorityQueue<Reading, (int Severity, long Sequence)> critical = new();
    // (Microsoft, n.d.b): LIFO Stack makes the latest override the first candidate for reversal.
    private readonly Stack<Command> undo = new();
    // (Microsoft, n.d.b): set membership suppresses duplicate active incidents.
    private readonly HashSet<string> alerts = new(StringComparer.OrdinalIgnoreCase);
    // (Microsoft, n.d.b): ordered keys preserve a global timestamp timeline.
    private readonly SortedDictionary<(DateTimeOffset Time, long Seq), Reading> timeline = new();
    private readonly Dictionary<string, AvlTree<(DateTimeOffset Time, long Seq), Reading>> indexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> patterns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AuditEntry> audit = [];
    private readonly List<Link> links = [];
    private readonly List<Attachment> attachments = [];
    private long sequence;
    public bool Paused { get; private set; }
    public string? StorageWarning { get; private set; }
    public static readonly LocationNode Locations = new("Facility A", new LocationNode("Zone 1", new LocationNode("Sub-Zone B"), new LocationNode("Sub-Zone C")), new LocationNode("Zone 2", new LocationNode("Lab")));
    public Gateway(bool seed = true) { if (seed) Seed(); }
    private void Log(string kind, string message)
    {
        audit.Add(new(DateTimeOffset.UtcNow, kind, message));
        if (audit.Count > 500) audit.RemoveRange(0, audit.Count - 500);
    }
    public static string Clean(string? text, string field, int max = 80)
    {
        var value = text?.Trim() ?? "";
        if (value.Length == 0 || value.Length > max || value.Any(char.IsControl)) throw new ArgumentException($"{field} must contain 1–{max} printable characters.");
        return value;
    }
    private Device Find(string id) => devices.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException("Unknown device ID.");
    public Device Register(Registration r)
    {
        lock (gate)
        {
            var id = Clean(r.Id, "ID", 40); var mac = Clean(r.Mac, "MAC", 17).ToUpperInvariant();
            if (!Regex.IsMatch(id, "^[a-zA-Z0-9-]+$")) throw new ArgumentException("ID may contain only letters, digits and hyphens.");
            if (!Regex.IsMatch(mac, "^([0-9A-F]{2}:){5}[0-9A-F]{2}$")) throw new ArgumentException("MAC must use AA:BB:CC:DD:EE:FF format.");
            if (devices.ContainsKey(id) || devices.Values.Any(d => d.Mac == mac)) throw new ArgumentException("ID and MAC must be unique.");
            if (devices.Count >= 200) throw new ArgumentException("Demo limit: 200 devices.");
            if (!Locations.Contains(r.Location)) throw new ArgumentException("Choose a location in Facility A.");
            if (!new[] { "Moisture", "Temperature", "Power", "Pump", "Relay", "Gateway" }.Contains(r.Category)) throw new ArgumentException("Invalid category.");
            var device = new Device(id, mac, Clean(r.Name, "Name"), r.Category, r.Location);
            devices[id] = device; indexes[id] = new(); Log("Registration", $"Registered {id} in {r.Location}."); return device;
        }
    }
    private bool IsCritical(Device d, double value) => d.Category switch { "Moisture" => value < 20, "Power" => value > 250, "Temperature" => value > 45, _ => false };
    public Reading Enqueue(TelemetryPacket<double> packet)
    {
        lock (gate)
        {
            var d = Find(packet.SensorId);
            if (!d.Online) throw new ArgumentException("Device is offline. Reconnect it before sending telemetry.");
            if (!double.IsFinite(packet.Payload) || packet.Payload < -100 || packet.Payload > 10000) throw new ArgumentException("Payload must be a finite value between -100 and 10000.");
            if (d.Category == "Moisture" && (packet.Payload < 0 || packet.Payload > 100)) throw new ArgumentException("Moisture must be between 0 and 100 percent.");
            if (standard.Count + critical.Count >= 10000) throw new ArgumentException("Queue is full; resume processing before sending more.");
            var time = packet.Timestamp?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
            if (time > DateTimeOffset.UtcNow.AddMinutes(5) || time < DateTimeOffset.UtcNow.AddYears(-1)) throw new ArgumentException("Timestamp must be within the past year and no more than 5 minutes ahead.");
            var reading = new Reading(++sequence, d.Id, packet.Payload, time, IsCritical(d, packet.Payload));
            // (Microsoft, n.d.a): min-heap, so lower severity numbers leave first. Sequence breaks ties FIFO.
            if (reading.Critical) critical.Enqueue(reading, (d.Category == "Power" ? 0 : 1, reading.Sequence));
            else standard.Enqueue(reading);
            return reading;
        }
    }
    public int Process(int max = 100)
    {
        lock (gate)
        {
            if (Paused) return 0;
            int processed = 0;
            while (processed < max)
            {
                Reading? r;
                if (!critical.TryDequeue(out r, out _) && !standard.TryDequeue(out r)) break;
                Index(r); var d = Find(r.SensorId);
                // Historical backfill must not overwrite a newer live sensor value.
                if (d.LastSeen is null || r.Timestamp >= d.LastSeen)
                {
                    devices[d.Id] = d with { Value = r.Value, LastSeen = r.Timestamp };
                    var key = $"threshold:{d.Id}";
                    if (r.Critical) { if (alerts.Add(key)) Log("Alert", $"Threshold exceeded on {d.Id}: {r.Value}."); }
                    else if (alerts.Remove(key)) Log("Recovery", $"Threshold recovered on {d.Id}.");
                }
                Log(r.Critical ? "Critical processed" : "FIFO processed", $"Packet #{r.Sequence}: {r.SensorId} = {r.Value}.");
                processed++;
            }
            // Bounded demo retention: trim in batches, then rebuild affected balanced indexes.
            if (timeline.Count > 50000)
            {
                foreach (var key in timeline.Keys.Take(timeline.Count - 45000).ToArray()) timeline.Remove(key);
                RebuildIndexes(); Log("Retention", "Retained latest 45,000 timestamped readings.");
            }
            return processed;
        }
    }
    private void Index(Reading r)
    {
        var key = (r.Timestamp, r.Sequence); timeline[key] = r;
        if (!indexes.TryGetValue(r.SensorId, out var tree)) indexes[r.SensorId] = tree = new();
        tree.Insert(key, r);
    }
    private void RebuildIndexes()
    {
        indexes.Clear(); foreach (var d in devices.Keys) indexes[d] = new();
        foreach (var r in timeline.Values) indexes[r.SensorId].Insert((r.Timestamp, r.Sequence), r);
    }
    public void SetPaused(bool paused) { lock (gate) { Paused = paused; Log("Processing", paused ? "Worker paused for queue demonstration." : "Worker resumed."); } }
    public Device SetOnline(string id, bool online)
    {
        lock (gate)
        {
            var d = Find(id); devices[d.Id] = d with { Online = online }; var key = $"offline:{d.Id}";
            if (!online && alerts.Add(key)) Log("Disconnect", $"{d.Id} offline. Routing recalculated without this node.");
            if (online && alerts.Remove(key)) Log("Reconnect", $"{d.Id} rejoined the mesh.");
            return devices[d.Id];
        }
    }
    public Command Execute(OverrideRequest request)
    {
        lock (gate)
        {
            var d = Find(request.SensorId);
            if (!d.Online) throw new ArgumentException("Cannot control an offline device.");
            if (d.Category is not ("Pump" or "Relay")) throw new ArgumentException("Only pumps and relays accept overrides.");
            if (undo.Count >= 1000) throw new ArgumentException("Undo history is full; undo commands before adding more.");
            var c = new Command(++sequence, d.Id, d.Enabled, request.Enabled, DateTimeOffset.UtcNow);
            undo.Push(c); devices[d.Id] = d with { Enabled = request.Enabled };
            foreach (var sensor in devices.Values.Where(s => s.Category == "Moisture" && s.Value < 20))
            { var key = $"{sensor.Id}|{d.Id}|{request.Enabled}"; patterns[key] = patterns.GetValueOrDefault(key) + 1; }
            Log("Command", $"#{c.Id}: {d.Id} → {(request.Enabled ? "ON" : "OFF")} (was {(d.Enabled ? "ON" : "OFF")})."); return c;
        }
    }
    public Command Undo()
    {
        lock (gate)
        {
            if (!undo.TryPeek(out var c)) throw new ArgumentException("No command to undo.");
            var d = Find(c.SensorId);
            if (!d.Online) throw new ArgumentException("Reconnect the last command's device before undoing it.");
            undo.Pop(); devices[d.Id] = d with { Enabled = c.Before };
            Log("Undo", $"Reverted #{c.Id}: {d.Id} restored to {(c.Before ? "ON" : "OFF")}."); return c;
        }
    }
    public void RecordSearch(string query)
    {
        lock (gate)
        {
            query = Clean(query, "Search", 80);
            // Record meaningful observed sensor→actuator searches, not every UI refresh.
            foreach (var sensor in devices.Values.Where(d => d.Category == "Moisture" && d.Value < 20))
            foreach (var target in devices.Values.Where(d => d.Category == "Pump" && (d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || d.Id.Contains(query, StringComparison.OrdinalIgnoreCase))))
            { var key = $"{sensor.Id}|{target.Id}|True"; patterns[key] = patterns.GetValueOrDefault(key) + 1; }
            Log("Search", query);
        }
    }
    private Insight[] Insights()
    {
        List<Insight> result = [];
        foreach (var d in devices.Values)
        {
            if (!d.Online) result.Add(new(d.Id, "Device offline", "Reconnect the node or inspect its power supply; routes exclude offline nodes.", null, null, 0));
            if (d.Online && d.Value is not null && IsCritical(d, d.Value.Value))
                result.Add(new(d.Id, "Threshold alert", $"Latest {d.Category.ToLowerInvariant()} value is {d.Value}. Inspect this device.", null, null, 0));
            var history = indexes[d.Id].Range((DateTimeOffset.UtcNow.AddMinutes(-10), long.MinValue), (DateTimeOffset.MaxValue, long.MaxValue)).Values.TakeLast(8).ToArray();
            if (d.Category == "Moisture" && d.Online && history.Length >= 4)
            {
                double step = (history[^1].Value - history[0].Value) / (history.Length - 1);
                if (step < -1 && history[^1].Value >= 20 && history[^1].Value + 3 * step < 20)
                    result.Add(new(d.Id, "Moisture trending down", $"Estimated next 3 samples cross 20% (trend {step:F1} points/sample). This heuristic is not a trained ML model.", null, null, history.Length));
            }
        }
        foreach (var pair in patterns.Where(p => p.Value >= 2))
        {
            var pieces = pair.Key.Split('|'); var sensor = Find(pieces[0]); var target = Find(pieces[1]); bool enable = bool.Parse(pieces[2]);
            if (sensor.Online && sensor.Value < 20 && target.Online && target.Enabled != enable)
                result.Add(new(sensor.Id, $"Suggested action: {target.Name} {(enable ? "ON" : "OFF")}", $"Observed {pair.Value} related searches/commands while {sensor.Name} was below 20%. Operator confirmation is required.", target.Id, enable, pair.Value));
        }
        return result.ToArray();
    }
    public object Dashboard()
    {
        lock (gate) return new {
            devices = devices.Values.ToArray(), alerts = alerts.Order().ToArray(), insights = Insights(),
            queue = new { standard = standard.Count, critical = critical.Count, paused = Paused },
            historyCount = timeline.Count, recent = timeline.Values.Reverse().Take(100).ToArray(),
            commands = undo.Take(100).ToArray(), undoCount = undo.Count, audit = audit.TakeLast(100).Reverse().ToArray(),
            attachments = attachments.Select(a => new { a.Id, a.SensorId, a.Name, a.Uploaded }).ToArray(), storageWarning = StorageWarning
        };
    }
    public Device[] Devices() { lock (gate) return devices.Values.ToArray(); }
    public HistoryResult History(string id, DateTimeOffset from, DateTimeOffset to)
    {
        lock (gate)
        {
            var d = Find(id); if (from > to) throw new ArgumentException("From must be before or equal to To.");
            var tree = indexes[d.Id]; var (values, visited) = tree.Range((from, long.MinValue), (to, long.MaxValue));
            return new(values.Take(5000).ToArray(), values.Count, visited, tree.Height, tree.Count);
        }
    }
    public void UpsertLink(Link edge)
    {
        lock (gate)
        {
            var from = Find(edge.From).Id; var to = Find(edge.To).Id;
            if (from == to || !double.IsFinite(edge.Weight) || edge.Weight <= 0 || edge.Weight > 10000) throw new ArgumentException("Use two different nodes and a positive cost up to 10,000.");
            links.RemoveAll(e => e.From == from && e.To == to || e.From == to && e.To == from);
            links.Add(new(from, to, edge.Weight)); Log("Mesh", $"Link {from} ↔ {to} cost {edge.Weight}.");
        }
    }
    public void RemoveLink(string from, string to) { lock (gate) { var a = Find(from).Id; var b = Find(to).Id; links.RemoveAll(e => e.From == a && e.To == b || e.From == b && e.To == a); Log("Mesh", $"Removed link {a} ↔ {b}."); } }
    public MeshResult Mesh(string source, string target)
    {
        lock (gate)
        {
            source = Find(source).Id; target = Find(target).Id;
            var graph = new MeshGraph(devices.Values, links); var reachable = graph.BreadthFirst(target);
            var (forest, components) = graph.MinimumSpanningForest(); var (route, cost) = graph.ShortestPath(source, target);
            return new(devices.Values.ToArray(), links.ToArray(), forest, reachable, devices.Keys.Except(reachable).ToArray(), route, cost, forest.Sum(e => e.Weight), components);
        }
    }
    public Attachment AddAttachment(string id, string name, string type, byte[] bytes)
    {
        lock (gate)
        {
            var d = Find(id);
            if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) throw new ArgumentException("Attachments must be 1 byte to 2 MiB.");
            if (attachments.Count >= 20) throw new ArgumentException("Demo attachment limit is 20.");
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (!new[] { ".txt", ".log", ".json", ".csv", ".png", ".jpg", ".jpeg", ".pdf" }.Contains(ext)) throw new ArgumentException("Use TXT, LOG, JSON, CSV, PNG, JPG or PDF.");
            var a = new Attachment(Guid.NewGuid().ToString("N"), d.Id, Path.GetFileName(name), type, Convert.ToBase64String(bytes), DateTimeOffset.UtcNow);
            attachments.Add(a); Log("Attachment", $"Added {a.Name} to {d.Id}."); return a;
        }
    }
    public Attachment GetAttachment(string id) { lock (gate) return attachments.FirstOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException("Attachment not found."); }
    public void Save(string path)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var state = new PersistedState(devices.Values.ToArray(), timeline.Values.ToArray(), links.ToArray(), undo.Reverse().ToArray(), audit.ToArray(), alerts.ToArray(), new(patterns), attachments.ToArray(), sequence);
                var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(state)); File.Move(temp, path, true); StorageWarning = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageWarning = "State could not be saved. Check data-folder permissions and disk space."; }
        }
    }
    public void Load(string path)
    {
        lock (gate)
        {
            if (!File.Exists(path)) return;
            try
            {
                var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(path)) ?? throw new JsonException();
                // Validate the persisted schema before replacing the working demo state.
                if (state.Devices is null || state.Readings is null || state.Links is null || state.UndoBottomFirst is null || state.Audit is null || state.Alerts is null || state.Patterns is null || state.Attachments is null) throw new JsonException();
                var ids = state.Devices.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (ids.Count != state.Devices.Length || !ids.Contains("GW-01") || state.Readings.Any(r => !ids.Contains(r.SensorId))) throw new JsonException();
                devices.Clear(); timeline.Clear(); links.Clear(); undo.Clear(); audit.Clear(); alerts.Clear(); patterns.Clear(); attachments.Clear();
                foreach (var d in state.Devices) devices[d.Id] = d;
                foreach (var r in state.Readings) timeline[(r.Timestamp, r.Sequence)] = r;
                links.AddRange(state.Links); foreach (var c in state.UndoBottomFirst) undo.Push(c);
                audit.AddRange(state.Audit); alerts.UnionWith(state.Alerts);
                foreach (var p in state.Patterns) patterns[p.Key] = p.Value;
                attachments.AddRange(state.Attachments); sequence = state.Sequence; RebuildIndexes(); Log("Startup", "Restored persisted state.");
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            { StorageWarning = "Saved state could not be read. The file was preserved; check or rename it before restarting."; throw new InvalidDataException(StorageWarning, ex); }
        }
    }
    private void Seed()
    {
        var rows = new[] { ("GW-01", "Central gateway", "Gateway", "Facility A"), ("MS-01", "Moisture Sensor B", "Moisture", "Sub-Zone B"), ("TP-01", "Greenhouse temperature", "Temperature", "Sub-Zone C"), ("PW-01", "Power monitor", "Power", "Lab"), ("PM-01", "Pump A", "Pump", "Sub-Zone B"), ("RL-01", "Ventilation relay", "Relay", "Sub-Zone C") };
        int n = 0; foreach (var (id, name, category, location) in rows) Register(new(id, $"AA:BB:CC:00:00:{++n:X2}", name, category, location));
        foreach (var edge in new[] { new Link("GW-01", "TP-01", 2), new("TP-01", "MS-01", 1), new("MS-01", "PM-01", 2), new("TP-01", "RL-01", 3), new("RL-01", "PW-01", 1), new("PW-01", "GW-01", 4), new("MS-01", "PW-01", 5), new("PM-01", "RL-01", 4) }) links.Add(edge);
        // Historical demo readings are explicitly labelled seeded data in the README and UI.
        for (int i = 0; i < 30; i++)
        foreach (var d in devices.Values.Where(d => d.Category is "Moisture" or "Temperature" or "Power").ToArray())
        {
            var value = d.Category switch { "Moisture" => 55 - i * 0.5, "Temperature" => 25 + Math.Sin(i / 3.0) * 2, _ => 220 + Math.Sin(i / 2.0) * 8 };
            Enqueue(new(d.Id, Math.Round(value, 2), DateTimeOffset.UtcNow.AddMinutes(-30 + i))); Process();
        }
        Log("Demo", "Seeded six simulated devices and 90 historical readings. Live simulation sends HTTP requests.");
    }
}
/* Reference list:
Microsoft (n.d.a) PriorityQueue<TElement,TPriority> Class. Available at:
https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2 (Accessed: 9 November 2026).
Microsoft (n.d.b) Collections and data structures. Available at:
https://learn.microsoft.com/dotnet/standard/collections/ (Accessed: 9 November 2026).
Queue, Stack, Dictionary, SortedDictionary and HashSet are framework collections; orchestration is original.
*/
