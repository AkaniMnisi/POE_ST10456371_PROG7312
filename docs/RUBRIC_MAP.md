# Supplied rubric → implementation evidence

Use this during a code walkthrough. The supplied task pages and later rubric distribute marks differently; this matrix covers both without inventing a revised mark allocation.

| Supplied criterion | Implementation | Presentation evidence |
|---|---|---|
| Main menu and asynchronous integration | Three active modules in `wwwroot/index.html`; hash navigation and Fetch in `app.js`; one C# host | Navigate during simulation; forms/state remain available |
| Sensor ingestion retained from Part 1 | Generic packet DTO, API validation, recursive locations, registration and attachments | Submit a reading; register a node; upload and download a log |
| Responsive command dashboard | Device state cards, category and alert filters, command actions, audit table | Search for Pump A and control it |
| FIFO message queue | `Gateway.standard`, `Enqueue`, `Process` | Pause worker, submit ordinary packets, resume |
| Critical priority queue / heaps | `Gateway.critical` with severity and sequence tuple | Critical packet submitted last is processed first |
| Stack and Undo | `Stack<Command>` stores before/after state; `Undo` restores dictionary state | ON → OFF → Undo returns ON |
| Dictionary lookup | `Dictionary<string,Device>` keyed by unique node ID | Backend lookup on every telemetry packet |
| Sorted dictionary | `timeline` indexed by timestamp and unique sequence | Chronological telemetry display; duplicate timestamps retained |
| HashSet | Distinct active threshold/offline alert keys | Repeated disconnect produces one active alert and one disconnect event |
| Pattern analysis and recommendation | Actual searches/commands counted against low-moisture context; threshold/trend rules | Send low moisture, search Pump A twice, Apply suggestion |
| Tree structures / historical range queries | Custom AVL with rotations, subtree pruning and per-sensor index | Query narrow range and show visited-node count, height and source |
| Graph modelling | Weighted undirected adjacency list, registered devices as vertices | Show mesh, add/remove link, identify node by ID/MAC |
| Traversal | BFS from selected destination | Reachable/unreachable list changes after disconnect |
| Minimum spanning tree | Lazy Prim heap traversal; forest for disconnected input | Seed MST cost 9; five links; overlay changes on node failure |
| Device-to-gateway path | Dijkstra with predecessor reconstruction | Initial cost 3, alternate cost 9 after TP-01 fails |
| README / data structure explanations | README sections 2–7 | Setup, usage, complexity table and code snippets |
| Completion report: challenges and learning | README section 9 | Concrete original defects and implemented responses; personalise any first-person reflection |
| Technology recommendations and rationale | README section 10 | Durable broker, time-series storage, SignalR, observability, security and scaling trade-offs |
| Iterative feedback incorporation | `IMPROVEMENT_LEDGER.md` | Real inspection findings and changes; lecturer feedback still needs to be supplied by the student |
| GitHub and collaboration | Preserve existing repository history when importing | Real repository link, contributors and meaningful commit history are external submission evidence |

## Items the code package cannot independently supply

The user's instruction excluded a separate Word/PDF document. The README provides technical report material, but cannot guarantee that the lecturer will accept it in place of a specifically requested separate report. No lecturer feedback, complete Part 1 brief, repository access, student identification or presentation recording was supplied. Do not present generated changes as pre-existing GitHub history or actual lecturer feedback. These limitations affect any proposed 100/100 claim.
