# Smart-X — IoT Operations Gateway

A complete local demonstration of sensor ingestion, real-time command processing and self-healing mesh routing for **PROG7312**.

**Start here:** extract the ZIP → install the **.NET 8 SDK** → double-click **`START_WINDOWS.cmd`** → wait for `Now listening on: http://localhost:5080` → open that address.

The application uses a C# ASP.NET Core backend and a responsive HTML/CSS/JavaScript client. All three modules run from one project and one URL. No SQL server, Azure account, ESP32 hardware, npm install, API key or external NuGet package is required. Simulation sends actual HTTP requests to the same API used by the UI.

## 1. What is included

| Module | Working features |
|---|---|
| Sensor telemetry | Device registration, unique ID/MAC validation, recursive location validation, typed generic telemetry packets, manually entered readings, optional timestamps, live HTTP simulator, threshold detection, file uploads/downloads, ordered telemetry feed |
| Command stream | Live device search, category and alert filters, FIFO message queue, severity/arrival-ordered priority heap, pause/resume demonstration, pump/relay ON/OFF commands, actual state restoration using stack-based Undo, deduplicated alerts, timestamped audit history |
| Automated insights | Threshold warnings, falling-moisture trend detection, associations learned from actual searches and manual commands; suggestions have an explicit Apply action |
| Network topology | Interactive weighted graph, node ID and MAC selection, BFS connectivity, Prim minimum spanning forest, Dijkstra route tracing, link editing/removal, disconnect/reconnect, automatic route recalculation |
| Historical data | Custom self-balancing AVL index per sensor, inclusive timestamp ranges, duplicate timestamp preservation, query diagnostics, time-series chart, table and CSV download |
| Engineering | Persistent JSON snapshots, restart recovery, validation, bounded demo storage, 31 executable backend checks, commented sources, reference lists, presentation walkthrough, rubric mapping and improvement ledger |

**Scope:** the application addresses the supplied Part 2 and final PoE screenshots and retains the Part 1 concepts visible in the uploaded project. A full Part 1 assessment brief was not supplied, so requirements not present in those materials cannot be certified. This package is not a guarantee of 100/100. The screenshots also assess report content, GitHub history and feedback incorporation; see section 12.

## 2. Run on Windows

### Option A — easiest launch

1. Download the ZIP and use **Extract All**. Do not launch from inside the compressed folder.
2. Move the extracted `SmartX_Complete` folder to a simple writable location, for example `C:\Projects\SmartX_Complete`.
3. Install the **.NET 8 SDK**, not only the runtime, from [Microsoft's .NET 8 download page](https://dotnet.microsoft.com/download/dotnet/8.0). Use the current patched SDK in that release family. This package was validated with SDK 8.0.408.
4. Open a new terminal and run `dotnet --list-sdks`. Confirm that an `8.0.xxx` SDK is listed.
5. Double-click `START_WINDOWS.cmd`.
6. Wait for the listening message, then open **http://localhost:5080** in Chrome or Edge. Keep the terminal open while demonstrating.
7. To stop, press **Ctrl+C** in the terminal. The hosted service drains pending packets and saves the state.

### Option B — Visual Studio

1. Install Visual Studio 2022 with the **ASP.NET and web development** workload and .NET 8 SDK support.
2. Open `SmartX.sln`.
3. Right-click **SmartX.Api** in Solution Explorer → **Set as Startup Project**.
4. Select the **SmartX** launch profile (not IIS Express).
5. Build the solution, then press **Ctrl+F5**. If the browser does not open automatically, visit http://localhost:5080.
6. The tests project is a standalone console test runner; run it using `TEST_WINDOWS.cmd` or the command below. It does not require a test-adapter package.

### Option C — terminal, macOS or Linux

From the extracted solution folder:

```bash
dotnet restore SmartX.sln
dotnet build SmartX.sln
dotnet run --project src/SmartX.Api
```

`NuGet.Config` clears package feeds intentionally: the projects use only SDK/shared-framework assemblies. Ensure .NET 8 SDK is installed locally. A later SDK alone may need the .NET 8 targeting packs, which cannot be downloaded through these cleared feeds.

## 3. Presentation walkthrough — approximately 10–12 minutes

Use a fresh demo state for predictable results. Stop the app and rename `src/SmartX.Api/App_Data/state.json` to `state.backup.json`, then restart. Do not rename a live state file. The first run creates six nodes and 90 clearly labelled historical demo readings.

### 0:00–1:00 — Introduce the architecture

Explain: “Smart-X models an IoT facility. A C# gateway validates incoming readings, queues processing, maintains device state and computes network routes. The browser is a client of that API.” Show all three navigation items. Navigation does not reload the document, so form selections and simulation state remain available.

### 1:00–2:30 — Sensor ingestion and attachments

1. In **Sensor telemetry**, click **Start live simulation**. Watch the history count and latest readings change. These are HTTP POSTs, not browser-only mock rows.
2. Switch to another module and back while simulation continues.
3. Click **Stop live simulation** before deterministic demonstrations.
4. Send value `42` for `MS-01`. Leave the timestamp blank for the current time. Wait up to two seconds for the UI to refresh.
5. Upload `docs/sample-device-log.txt` to `MS-01`, then download it from the attachment link.
6. Optionally register `MS-02`, MAC `AA:BB:CC:00:00:07`, category `Moisture`, location `Sub-Zone C`. Explain that a newly registered node has no wireless link until one is added in topology.

### 2:30–4:00 — Queue and priority processing

1. Open **Command stream** and click **Queue priority demo**. This stops the simulator, pauses the worker, and submits ordinary temperature and power readings followed by critical moisture.
2. Confirm pending counts: two FIFO and one priority packet, assuming the queues were empty beforehand.
3. Click **Resume worker**. Pending counts return to zero.
4. Inspect **Operational audit**. Read the processing events from oldest to newest: the critical moisture packet is processed before the two ordinary packets, despite arriving last.
5. Explain that the table is displayed newest-first; it is not a processing-order diagram.
6. Point to `Gateway.Enqueue` and `Gateway.Process` for the actual `Queue<T>` and `PriorityQueue<T,TPriority>` use.

### 4:00–5:00 — Command stack and state restoration

1. Select **Pump A** and click **Turn ON**. The live device card changes from OFF to ON.
2. Click **Turn OFF**, then **Undo last**. The pump returns to ON.
3. Click **Undo last** again. It returns to its original OFF state.
4. Explain why a stack is appropriate: the most recent command is reversed first, and each command stores its before/after states. This is more than removing a line from a history list.

### 5:00–6:00 — Predictive actions and sets

1. Click **Send low moisture**. The worker must be running. Wait for the reading to be processed.
2. Enter `Pump A` in the search box and press **Search** twice. Actual search events are recorded; merely refreshing the page is not counted.
3. **Suggested actions** displays a learned association with evidence count. Click **Apply suggestion**. Pump A turns ON.
4. Explain that this is an interpretable rule/association engine, not a trained machine-learning model or autonomous hardware controller.
5. Clear the search box. Disconnect `TP-01`. The active alert count increases once. Repeated offline API requests do not produce duplicate disconnect events because the `HashSet` already contains that alert key.

### 6:00–8:00 — Network routing and self-healing

1. With all six default devices online and unchanged links, select source `MS-01` and gateway `GW-01` in **Network topology**. The shortest route is `MS-01 → TP-01 → GW-01`, cost **3**.
2. The initial minimum-spanning-tree total is **9**, comprising five links. Toggle the MST highlight to compare the overlay.
3. If `TP-01` is disconnected, the route becomes `MS-01 → PW-01 → GW-01`, cost **9**. The diagram, BFS reachability and route update without a restart.
4. Reconnect `TP-01` in Command stream to restore the original route.
5. Explain the distinction: **Prim minimises the total cost of connecting the network; Dijkstra minimises the cost of one source-to-gateway route.** An MST is not necessarily the shortest route for every device.
6. For an optional isolation demonstration, disconnect both `TP-01` and `PW-01`. The gateway has no route to the remaining cluster. The app shows “No route available” and a forest with multiple components, rather than inventing a connection. Reconnect both afterwards.
7. If you registered `MS-02`, add a link from it to `TP-01` with cost `3`. Select the new node to see its route.

### 8:00–9:30 — AVL historical queries

1. Select `MS-01` under **Historical telemetry query**.
2. Leave dates empty and click **Query history** to show retained data, the chart, tree height and visited-node count.
3. Enter a narrow time range around a recent reading and query again. Only matching timestamps are returned. Inputs use local time; the API receives ISO UTC instants.
4. Click **Export CSV**. Explain the inclusive boundaries and `(timestamp, sequence)` composite key that keeps simultaneous readings distinct.
5. Open `Algorithms/AvlTree.cs`. Explain height maintenance, left/right rotations and pruning of subtrees outside the requested range.

### 9:30–11:00 — Verification and close

Run `TEST_WINDOWS.cmd` to show the 31 checks. The stress case builds a 100,000-entry AVL tree and compares a narrow query against its expected values. Discuss measured output as evidence from that machine, not a universal speed guarantee. Show the improvement ledger, references and future scaling rationale. Finish with the limitations in section 11.

## 4. Architecture and source guide

| File | Responsibility |
|---|---|
| `src/SmartX.Api/Program.cs` | Single-origin HTTP endpoints, static hosting, validation error responses and upload handling |
| `Models/Models.cs` | Immutable device/readings/commands, generic telemetry DTO, recursive location tree and overloaded `SensorData` operators |
| `Services/Gateway.cs` | State coordination, queues, indexes, commands, alert lifecycle, insights, persistence and validation |
| `Services/ProcessingWorker.cs` | Background processing every 500 ms; snapshots every five seconds; graceful shutdown drain |
| `Algorithms/AvlTree.cs` | Original generic AVL implementation, rotations and inclusive range search |
| `Algorithms/MeshGraph.cs` | Adjacency lists, BFS, lazy Prim forest and Dijkstra route |
| `wwwroot/index.html`, `app.css`, `app.js` | Unified responsive client, HTTP simulation, forms, SVG topology and history chart |
| `tests/SmartX.Tests/Program.cs` | Dependency-free assertion runner with algorithm, state and persistence checks |

Request flow: browser or device simulator → HTTP endpoint → validation → queue → background worker → sorted timeline and per-sensor AVL index → dashboard/history API → UI. Commands use a separate synchronous path so an override updates the simulated actuator immediately.

The original upload contained two Blazor client projects with conflicting ports and disabled modules. This completion consolidates them into a single ASP.NET Core project with a static asynchronous web client, while preserving the C# backend and the sensor/command concepts. This is an intentional refactor, not a claim that the original solution was already complete. The supplied instructions permit a web frontend.

## 5. Why these data structures are used

Complexities below describe the individual structures, not the cost of the complete HTTP request, JSON serialization or SVG rendering. `n` is the index size, `k` returned readings, `V` online vertices and `E` usable links.

| Structure | Actual use | Typical complexity and trade-off |
|---|---|---|
| `Queue<Reading>` | Ordinary incoming telemetry in arrival order | Amortised O(1) enqueue/dequeue; critical events take precedence |
| `PriorityQueue<Reading,(int,long)>` | Critical packets ordered by severity then arrival sequence | O(log n) insertion/removal; .NET uses a heap (Microsoft, n.d.a) |
| `Stack<Command>` | Undo with stored previous actuator state | O(1) push/pop; no redo; blocked undo does not remove the entry |
| `Dictionary<string,Device>` | Registered device lookup by unique ID | Expected O(1); MAC uniqueness is checked during registration |
| `SortedDictionary<(DateTimeOffset,long),Reading>` | Ordered global telemetry timeline | O(log n) insertion; full newest-first snapshot enumeration is O(n), so retention is bounded |
| `HashSet<string>` | Unique `offline:ID` and `threshold:ID` active states | Expected O(1) add/remove; recovery removes the key so a later new incident can be logged |
| Recursive location tree | Facility → zone → subzone validation | O(number of locations); supports nested leaves, fixing the uploaded validation failure |
| Custom AVL tree per sensor | Timestamp-filtered historical queries | O(log n) insertion; O(log n + k) range work; O(n) storage; rotations prevent sorted arrivals degenerating into a chain |
| Adjacency-list graph | Undirected wireless connections | O(V+E) storage; offline nodes/incident links are excluded from the active graph |
| BFS queue and visited set | Connectivity from selected gateway | O(V+E); lists unreachable/offline devices explicitly |
| Prim + heap | Minimum spanning forest | O(E log E) for this lazy implementation; processes every component |
| Dijkstra + heap | Minimum-cost route to gateway | O((V+E) log E) upper bound with duplicate heap entries; positive link weights are validated |

### AVL example

```csharp
var key = (reading.Timestamp, reading.Sequence);
tree.Insert(key, reading);
var result = tree.Range((from, long.MinValue), (to, long.MaxValue));
```

`Range` compares its bounds against each visited key. It follows the left branch only when that branch could contain values after `from`, and the right branch only when it could contain values before `to`. Results are emitted in sorted order. Height updates and single/double rotations keep balance factors within one. A per-sensor dictionary avoids first scanning unrelated sensors. The UI shows visited nodes, height and index size so efficiency can be discussed using evidence.

### Prim and route example

```csharp
var graph = new MeshGraph(devices, links);
var connected = graph.BreadthFirst("GW-01");
var (forest, components) = graph.MinimumSpanningForest();
var (path, cost) = graph.ShortestPath("MS-01", "GW-01");
```

Prim adds the cheapest edge crossing the visited/unvisited boundary, ignoring stale heap edges whose destination is already visited. The implementation restarts for disconnected components. BFS checks reachability; Dijkstra maintains cumulative distances and predecessor links to reconstruct a route. These are separate responsibilities (Sedgewick and Wayne, 2011b–d).

### Insight logic

Threshold rules are moisture below 20%, temperature above 45°C and power above 250V. For moisture trends, the engine uses up to eight readings from the last ten minutes and estimates change per sample. If the next three estimated samples cross 20%, it raises an early warning. This assumes roughly regular sample spacing and is a heuristic.

Searches matching a pump and manual commands observed while a moisture sensor is below 20% increment `(sensor, actuator, requested state)` counters. At least two observations are needed for a suggestion. Recommendations disappear when the condition is no longer active, the actuator already has that state, or either device is offline. Threshold and trend warnings can appear without behavioural history. A user must click Apply for a command to execute.

## 6. Test and evidence

```bash
dotnet build SmartX.sln -c Release
dotnet run --project tests/SmartX.Tests -c Release
```

The console runner returns exit code 1 if any check fails. It is intentionally independent of xUnit/NUnit downloads. Tests cover all four AVL rotation cases, random ranges compared with a sorted baseline, duplicate timestamps, priority ordering, FIFO ties, queue pause/resume, validation, commands/undo, alert deduplication and recovery, learned actions, trends, known MST and route costs, disconnected graphs, attachments, restart recovery, corrupt-file preservation and concurrent ingestion.

See `docs/VALIDATION.md` for the exact checks completed in the build environment and any testing limitations. Run the app and the walkthrough on your presentation laptop before assessment; a Linux build does not itself certify your Windows installation.

For an additional real HTTP telemetry sender, keep the app running and use:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Simulate.ps1 -Count 12
```

The script uses `Invoke-RestMethod` against `/api/telemetry/ingest`. An API collection is provided in `docs/SmartX.http` for manual inspection with a compatible HTTP client.

## 7. API quick reference

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/health` | Health check |
| GET | `/api/state` | Device/queue/insight/audit snapshot |
| GET / POST | `/api/devices` | List/register devices |
| POST | `/api/devices/{id}/online` | `{ "online": false }` simulates node failure |
| POST | `/api/telemetry/ingest` | `{ "sensorId": "MS-01", "payload": 15 }`; optional ISO `timestamp` |
| POST | `/api/processing` | `{ "paused": true }` |
| POST | `/api/commands` | `{ "sensorId": "PM-01", "enabled": true }` |
| POST | `/api/commands/undo` | Restore most recent command's previous state |
| POST | `/api/search` | `{ "query": "Pump A" }` records a meaningful search |
| GET | `/api/history/{id}?from=...&to=...` | Inclusive AVL range query; dates optional |
| GET | `/api/topology?source=MS-01&target=GW-01` | Graph, BFS, forest and shortest route |
| POST / DELETE | `/api/links` | Save weighted link / remove using `from` and `to` query parameters |
| POST | `/api/attachments/{deviceId}` | Multipart form field named `file` |
| GET | `/api/attachments/{attachmentId}` | Download original bytes |

Ingestion returns **202 Accepted**: the packet is queued, not necessarily processed yet. Registration/upload returns 201; invalid input returns 400; unknown devices/endpoints return 404. A reading's critical classification is calculated on the server from the registered category; clients cannot bypass thresholds by setting a flag.

## 8. Persistence and reset

State is stored in `src/SmartX.Api/App_Data/state.json` when launched with the project. It includes devices, processed readings, links, undo commands, active alerts, learned pattern counters, attachments and recent audit history. Startup rebuilds tree indexes from the saved readings. Temporary-file replacement avoids writing a partial snapshot over the previous one. A malformed file stops startup with an explanatory error and is preserved for investigation.

Snapshots occur every five seconds and on graceful shutdown. Pending queues are in memory and are drained on graceful shutdown; a forced process kill can lose unprocessed packets and changes since the last snapshot. This is suitable for a local demonstration, not a durable message broker.

To reset safely: stop the application, back up/rename the state file, then restart. The six-node seed is created when no state file exists. The ZIP deliberately excludes test-generated `App_Data`, `bin`, `obj` and IDE cache folders.

## 9. Implementation challenges and completion review

The inspection and changes are recorded in `docs/IMPROVEMENT_LEDGER.md`. They are engineering observations, not invented lecturer feedback.

* **Integration:** the uploaded clients targeted different API ports. A single origin removes that dependency and avoids permissive CORS. A static client also avoids contradictory Blazor package versions.
* **Validation:** the original facility dictionary recursion did not traverse nested zone entries correctly. A recursive `LocationNode` model now reaches actual leaf locations and rejects unknown locations.
* **Concurrency:** HTTP handlers and the background worker access the same structures. A single lock protects compound operations, such as changing device state and updating its alert. This favours correctness and explainability for a local gateway; it limits throughput at scale.
* **Historical indexing:** timestamp-only keys would overwrite simultaneous readings. Composite sequence keys preserve every accepted packet. Late historical readings are indexed but cannot overwrite a newer live value.
* **Undo correctness:** a message saying “reverted” is not enough. Commands retain old state, and Undo updates the same device dictionary used by the dashboard. An offline actuator blocks Undo until it reconnects, preserving the stack entry.
* **Disconnected networks:** failing a node can split the graph. BFS and Dijkstra handle absence explicitly; Prim returns a forest rather than claiming the whole mesh is connected.
* **Presentation reliability:** explicit queue pause/resume makes short-lived queue behaviour observable. The simulator can be stopped before reproducing a threshold or routing example.

These changes illustrate how data structures support application behaviour: a tree is useful only when the history endpoint queries it; a stack is useful only when it restores state; a graph must be the source of the displayed route. Review the code and replace any personal reflection required by your institution with your own experience.

## 10. Future scaling and technology recommendations

These are proposed extensions, not implemented features.

| Extension | Why it would help this design | Trade-off / implementation consideration |
|---|---|---|
| Durable broker such as Azure Service Bus | Persist accepted messages, retry failed processing and separate ingestion from workers | Requires idempotency keys, dead-letter handling, credentials and cost control; priority semantics need explicit design |
| MQTT/IoT gateway integration | Replace the HTTP simulator with actual device sessions and acknowledgements | Device provisioning, topic authorisation, reconnect behaviour and protocol translation become necessary |
| PostgreSQL/time-series storage or partitioned SQL | Durable history beyond the local 50,000-reading limit and efficient long-range queries | Database indexes complement the educational AVL structure; avoid duplicating unlimited history in application memory |
| SignalR push notifications | Reduce repetitive two-second polling and deliver incremental device updates | Requires reconnection handling, backpressure and multi-instance connection coordination |
| Object storage for attachments | Keep large binaries out of full JSON snapshots | Use scoped download links and malware scanning; maintain device metadata separately |
| Containerised workers with orchestration | Scale ingestion/processing independently and make deployment repeatable | State must move out of process first; replicas cannot each maintain an independent authoritative command stack |
| OpenTelemetry metrics and traces | Measure queue latency, processing rate, route computation and persistence failures | Define useful metrics and sampling limits; avoid logging secrets or oversized packet bodies |
| Authentication, role policies and HTTPS | Limit overrides to authorised operators and separate read-only viewers | Required before exposing this localhost demonstration to a shared or public network |

Optimise from measurements: the educational AVL query is efficient, but full-state JSON serialization, one global lock, snapshot writes and large SVG graphs become bottlenecks before tree lookup does. Production improvements should target those paths rather than claiming that choosing a tree makes the entire system infinitely scalable.

## 11. Limits and troubleshooting

* **Local simulation:** no physical ESP32 acknowledgement, real actuator control, authentication or multi-tenant access. The default launch is localhost. Do not expose it publicly unchanged.
* **Bounds:** at most 200 registered devices, 10,000 pending readings, 1,000 undo entries and 20 attachments of 2 MiB each. Telemetry is trimmed from over 50,000 readings to the latest 45,000 by timestamp. Audit storage retains 500 entries, and the UI shows the latest 100. Historical responses/export show at most the first 5,000 matches and report the total.
* **Priority policy:** critical packets always precede normal ones; sustained critical traffic can starve FIFO work. Production scheduling should add a fairness policy.
* **Visual scale:** graph algorithms support dynamic nodes, but the simple radial SVG layout is intended for small demonstration networks; many labels can overlap. The 100,000-entry test concerns historical indexing, not 100,000 rendered devices.
* **“dotnet is not recognised”:** install the SDK and reopen the terminal.
* **Missing framework/targeting pack:** install .NET 8 SDK even if another SDK is already installed. The package clears external NuGet feeds.
* **Port already in use:** stop the previous instance, or run `dotnet run --project src/SmartX.Api --no-launch-profile --urls http://localhost:5081` and open that URL. Relative API URLs continue working.
* **Nothing updates:** keep the server terminal open, check the connection badge, and ensure the processing worker is not paused. Use Ctrl+F5 in the browser after editing client files.
* **No learned recommendation:** stop simulation, set Pump A OFF, send moisture 15, wait for processing, then submit `Pump A` twice with the Search button.
* **No route:** check online state and links. Registration alone does not connect a device to the mesh.
* **No historical matches:** clear date filters and query again. The initial seed covers about the previous 30 minutes.
* **Save warning:** move the project to a writable folder and check disk space. Do not run from inside the ZIP or a protected system directory.
* **Startup rejects state file:** preserve/rename the file while the app is stopped, then restart with fresh demo data.

## 12. Submission and rubric checklist

See `docs/RUBRIC_MAP.md` for the requirement-to-code mapping. No Word/PDF submission report is included, as requested. This README contains the implementation explanations, completion review and future recommendations so it can support your presentation; confirm whether the lecturer requires a separate report upload.

Before submission:

- Rehearse the walkthrough on your own computer and understand the algorithms.
- Add your actual student/group details where required; no identity has been inferred from the archive filename.
- Capture your own running screenshots and preserve the test output.
- Add the code to your existing GitHub repository without deleting its `.git` folder. The ZIP excludes Git metadata and does not fabricate commits or collaborators.
- The supplied rubric mentions **25+ well-structured commits and collaboration evidence**. That evidence must reflect real work and contributors; adding this ZIP as one commit cannot retroactively create it.
- If you received lecturer feedback, add its actual wording and the implemented response to the ledger. No lecturer feedback was supplied here.
- Check any separate report/declaration/submission requirements outside the screenshots. Follow your institution's acknowledgement requirements for external and AI assistance.

## 13. References and attribution

Inline author–date comments appear beside relevant implementations, and source files contain comment reference lists. Code-specific explanations are original; references identify the framework documentation and algorithm concepts consulted rather than claiming copied implementations.

MDN contributors (n.d.a) *Using the Fetch API*. Available at: https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch (Accessed: 9 November 2026).

MDN contributors (n.d.b) *Canvas API*. Available at: https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API (Accessed: 9 November 2026).

Microsoft (n.d.a) *PriorityQueue<TElement,TPriority> Class*. Available at: https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2 (Accessed: 9 November 2026).

Microsoft (n.d.b) *Collections and data structures*. Available at: https://learn.microsoft.com/dotnet/standard/collections/ (Accessed: 9 November 2026).

Microsoft (n.d.c) *Background tasks with hosted services in ASP.NET Core*. Available at: https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services (Accessed: 9 November 2026).

Microsoft (n.d.d) *APIs overview*. Available at: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview (Accessed: 9 November 2026).

Microsoft (n.d.e) *Upload files in ASP.NET Core*. Available at: https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads (Accessed: 9 November 2026).

Sedgewick, R. and Wayne, K. (2011a) *Algorithms, 4th edition: Searching*. Available at: https://algs4.cs.princeton.edu/30searching/ (Accessed: 9 November 2026). Used for balanced-index principles; the project's AVL rotations are an original implementation, not a copy of the authors' red-black tree.

Sedgewick, R. and Wayne, K. (2011b) *Algorithms, 4th edition: Undirected graphs*. Available at: https://algs4.cs.princeton.edu/41graph/ (Accessed: 9 November 2026).

Sedgewick, R. and Wayne, K. (2011c) *Algorithms, 4th edition: Minimum spanning trees*. Available at: https://algs4.cs.princeton.edu/43mst/ (Accessed: 9 November 2026).

Sedgewick, R. and Wayne, K. (2011d) *Algorithms, 4th edition: Shortest paths*. Available at: https://algs4.cs.princeton.edu/44sp/ (Accessed: 9 November 2026).

Project input: `AkaniMnisiPROG7312-Part1.zip` and the user-supplied PROG7312 assessment/rubric screenshots. This completion refactors and extends their concepts; preserve the original repository's authorship and history when integrating it.
