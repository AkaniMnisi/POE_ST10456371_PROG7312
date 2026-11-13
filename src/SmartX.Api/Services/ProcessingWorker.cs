namespace SmartX.Services;
// (Microsoft, n.d.c): hosted services run work independently of HTTP requests.
public sealed class ProcessingWorker(Gateway gateway, IWebHostEnvironment environment) : BackgroundService
{
    private readonly string statePath = Path.Combine(environment.ContentRootPath, "App_Data", "state.json");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        int ticks = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            { gateway.Process(); if (++ticks % 10 == 0) gateway.Save(statePath); }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken token)
    {
        await base.StopAsync(token); gateway.SetPaused(false); gateway.Process(10000); gateway.Save(statePath);
    }
}
/* Reference list: Microsoft (n.d.c) Background tasks with hosted services in ASP.NET Core.
https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services (Accessed: 9 November 2026). */
