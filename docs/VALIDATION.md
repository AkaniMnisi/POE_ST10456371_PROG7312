# Validation record

**Date:** 9 November 2026. **Environment:** Linux x64, .NET SDK 8.0.408, Node.js 24.22.1.

## Completed checks

| Check | Observed result |
|---|---|
| Debug and Release solution builds | Succeeded; zero warnings and zero errors |
| C# algorithm/service assertion runner | 31 passed; zero failed |
| Live HTTP smoke suite against a running ASP.NET Core process | 18 passed |
| JavaScript syntax check (`node --check`) | Passed |
| Persistence roundtrip | Devices, readings, links, attachments and Undo restored |
| Corrupt-file behaviour | Rejected without overwriting the invalid file |
| Concurrent ingestion | 1,000 concurrent accepted readings retained without duplicate sequence IDs |
| AVL stress case | 100,000 keys; height 17; 11-match range visited 25 nodes |

The Release solution build used one MSBuild worker (`-m:1 --disable-build-servers`) because this execution environment restricts build-server sockets. The normal Windows commands in the README do not require this environment-specific workaround.

The initial observed stress run completed insertion plus query in 118 ms. Timing varies by hardware and load; it is not a promised response time or a comparison benchmark. The correctness checks assert sorted results, balance and pruning rather than a fragile timing threshold.

The 18 HTTP checks cover health, HTML/CSS/JavaScript delivery, missing endpoints, invalid moisture, paused queue acceptance, background consumption, priority ordering, Undo state restoration, search-derived recommendations, initial and alternate route costs, history metadata, invalid dates, device registration, multipart upload and exact downloaded bytes.

## Verification limitations

A headless Chromium walkthrough was attempted, but the execution environment prevented Chromium from creating its process-singleton socket (`Operation not permitted`). Consequently, a full browser interaction run, screenshot-based layout inspection and a Windows/Visual Studio launch were **not completed here**. Do not interpret successful backend and HTTP checks as proof of those unperformed checks.

The README's presentation sequence is the manual browser acceptance checklist. Rehearse it on the actual presentation laptop, including a narrow history query, CSV download, navigation during simulation, mobile/narrow layout if relevant and restarting the application with saved state.

This package does not claim physical ESP32 tests, production load testing, public deployment security validation or assessment marks.

## Repeat the checks

From the solution folder:

```bash
dotnet build SmartX.sln -c Release -m:1 --disable-build-servers
dotnet run --project tests/SmartX.Tests -c Release
```

For the optional HTTP checks, start the application in a separate terminal and run:

```bash
python scripts/api_smoke.py http://localhost:5080
```

Python 3 is required only for this optional smoke script, not for the application or C# tests. It intentionally creates a test device and attachment. Use fresh demo data to reproduce the known initial graph costs. Stop the app and rename its state file before rehearsing from a fresh seed.
