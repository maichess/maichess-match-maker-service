using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal sealed class MatchingWorker(MatchingService service) : BackgroundService
{
    private static readonly string[] TimeControls = ["bullet", "blitz", "rapid", "classical"];

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (string timeControl in TimeControls)
            {
                await service.TryMatchAsync(timeControl, stoppingToken);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
