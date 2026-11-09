# Engineering improvement ledger

Date: 9 November 2026. Source: inspection of the uploaded project and supplied assessment screenshots. These are observed defects and implementation decisions, **not lecturer comments**.

| ID | Observed gap / requirement | Completed change | Verification |
|---|---|---|---|
| 01 | Duplicate `SmartX.Client` / `SmartX.Clients` projects and mismatched API ports | One ASP.NET Core host with same-origin client requests | Solution build and HTTP checks |
| 02 | Home menu marked command/topology modules disabled | All three modules active with hash navigation | Source inspection; browser rehearsal still required |
| 03 | Recursive facility lookup could not descend into nested dictionary zones | Recursive `LocationNode.Children` traversal | Leaf and unknown-location checks |
| 04 | Telemetry UI relied on its own local packet list | API snapshot is the authoritative device/history source | Live HTTP ingestion and history checks |
| 05 | Registration flow absent from the UI | Validated ID/MAC/name/category/location form and API | Registration checks |
| 06 | Queue structures existed without complete observable worker integration | Background worker and pause/resume controls | Critical-before-FIFO, severity and tie checks |
| 07 | Undo returned a message without restoring actuator state | Store previous state in command stack and restore it | Two-level LIFO and offline-block checks |
| 08 | Displayed device list was hard-coded | API-backed searchable live registry | API registration and state checks |
| 09 | Repeated error states needed deduplication | HashSet alert lifecycle and recovery | Duplicate disconnect and recovery checks |
| 10 | Recommendations needed evidence from operator behaviour | Context-keyed search/command counters and trend heuristic | Search, manual-command and trend checks |
| 11 | Network topology module missing | Adjacency graph with BFS, Prim and Dijkstra plus SVG view | Known-cost, rerouting and disconnected-forest checks |
| 12 | Timestamp range tree absent | Generic AVL with inclusive pruned traversal | Random baseline, rotation and 100,000-entry checks |
| 13 | Duplicate timestamps could overwrite readings | Composite timestamp/sequence key | Duplicate-timestamp check |
| 14 | Late historical readings could regress device state | Update live value only when timestamp is not older | Backfill check |
| 15 | Attachment API not complete | Bounded multipart upload, device metadata and download | Byte-for-byte roundtrip checks |
| 16 | No restart recovery for live structures | Atomic JSON snapshot and index reconstruction | Save/load, undo-after-load and corrupt-file checks |
| 17 | Multiple requests could mutate non-thread-safe structures | Lock-protected compound state transitions | Parallel ingestion of 1,000 packets |
| 18 | README did not explain a complete final app | Setup, demo script, complexity, API, completion review and future design rationale | Requirement-to-section review |
| 19 | Inline source acknowledgements missing | Author–date comments and code comment reference lists | Reference/source inspection |
| 20 | Demo state could accumulate without limits | Explicit queue, history, audit, undo and attachment bounds | Source review; limits documented |

## Student feedback log — complete only with genuine feedback

Add rows here when actual lecturer or peer feedback is available. Include date, exact feedback or an accurate attributed summary, affected requirement, change, commit and verification. No feedback is invented in this package.

| Date and source | Feedback | Response / files | Actual commit | Evidence |
|---|---|---|---|---|
| Not yet supplied | — | — | — | — |
