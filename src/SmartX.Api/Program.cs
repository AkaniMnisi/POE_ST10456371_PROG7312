using SmartX.Models;
using SmartX.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ =>
{
    var gateway = new Gateway();
    gateway.Load(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "state.json"));
    return gateway;
});
builder.Services.AddHostedService<ProcessingWorker>();
// No external NuGet packages: the API and static client share one origin and one process.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 3 * 1024 * 1024);
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    try { await next(); }
    catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or BadHttpRequestException)
    {
        context.Response.StatusCode = ex is KeyNotFoundException ? 404 : 400;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "Smart-X Gateway" }));
app.MapGet("/api/state", (Gateway g) => g.Dashboard());
app.MapGet("/api/devices", (Gateway g) => g.Devices());
app.MapPost("/api/devices", (Registration r, Gateway g) => Results.Created($"/api/devices/{r.Id}", g.Register(r)));
app.MapPost("/api/devices/{id}/online", (string id, OnlineRequest r, Gateway g) => g.SetOnline(id, r.Online));
app.MapPost("/api/telemetry/ingest", (TelemetryPacket<double> r, Gateway g) => Results.Accepted(value: g.Enqueue(r)));
app.MapPost("/api/processing", (ProcessingRequest r, Gateway g) => { g.SetPaused(r.Paused); return Results.Ok(new { r.Paused }); });
app.MapPost("/api/commands", (OverrideRequest r, Gateway g) => g.Execute(r));
app.MapPost("/api/commands/undo", (Gateway g) => g.Undo());
app.MapPost("/api/search", (SearchRequest r, Gateway g) => { g.RecordSearch(r.Query); return Results.Ok(new { recorded = true }); });
app.MapGet("/api/history/{id}", (string id, DateTimeOffset? from, DateTimeOffset? to, Gateway g) => g.History(id, from ?? DateTimeOffset.MinValue, to ?? DateTimeOffset.MaxValue));
app.MapGet("/api/topology", (string? source, string? target, Gateway g) => g.Mesh(source ?? "MS-01", target ?? "GW-01"));
app.MapPost("/api/links", (Link edge, Gateway g) => { g.UpsertLink(edge); return Results.Ok(new { saved = true }); });
app.MapDelete("/api/links", (string from, string to, Gateway g) => { g.RemoveLink(from, to); return Results.Ok(new { removed = true }); });
// Multipart parsing is explicit; files are stored as data outside wwwroot and downloaded as attachments.
app.MapPost("/api/attachments/{id}", async (string id, HttpRequest request, Gateway g) =>
{
    if (!request.HasFormContentType) throw new ArgumentException("Send multipart form-data with a file field.");
    var form = await request.ReadFormAsync(); var file = form.Files.GetFile("file") ?? throw new ArgumentException("Choose a file.");
    if (file.Length > 2 * 1024 * 1024) throw new ArgumentException("Maximum file size is 2 MiB.");
    using var memory = new MemoryStream(); await file.CopyToAsync(memory);
    var saved = g.AddAttachment(id, file.FileName, file.ContentType, memory.ToArray());
    return Results.Created($"/api/attachments/{saved.Id}", new { saved.Id, saved.Name });
});
app.MapGet("/api/attachments/{id}", (string id, Gateway g) =>
{
    var a = g.GetAttachment(id); return Results.File(Convert.FromBase64String(a.Base64), "application/octet-stream", a.Name);
});
app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "API endpoint not found." }));
app.Run();
/* Reference list: Microsoft (n.d.d) Minimal APIs overview.
https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview (Accessed: 9 November 2026).
Microsoft (n.d.e) Upload files in ASP.NET Core.
https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads (Accessed: 9 November 2026). */
