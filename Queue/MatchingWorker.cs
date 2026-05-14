using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal sealed class MatchingWorker(MatchingService service) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (Maichess.MatchManager.V1.TimeFormat preset in TimeFormatRegistry.Presets)
            {
                await service.TryMatchAsync(preset.Id, stoppingToken);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
