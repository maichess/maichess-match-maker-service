using Maichess.MatchManager.V1;
using MatchManagerTimeControl = Maichess.MatchManager.V1.TimeControl;

namespace MaichessMatchMakerService.Queue;

internal sealed class MatchingWorker(
    QueueRepository queue,
    Matches.MatchesClient matchesClient,
    ILogger<MatchingWorker> logger) : BackgroundService
{
    private static readonly string[] TimeControls = ["bullet", "blitz", "rapid", "classical"];

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (string timeControl in TimeControls)
            {
                await TryMatchAsync(timeControl, stoppingToken);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private static MatchManagerTimeControl MapTimeControl(string value) => value switch
    {
        "bullet" => MatchManagerTimeControl.Bullet,
        "blitz" => MatchManagerTimeControl.Blitz,
        "rapid" => MatchManagerTimeControl.Rapid,
        "classical" => MatchManagerTimeControl.Classical,
        _ => MatchManagerTimeControl.Unspecified,
    };

    private async Task TryMatchAsync(string timeControl, CancellationToken ct)
    {
        long count = await queue.GetQueueCountAsync(timeControl);
        if (count < 2)
        {
            return;
        }

        string[] tokens = await queue.DequeueOldestPairAsync(timeControl);
        if (tokens.Length < 2)
        {
            return;
        }

        QueueEntry? white = await queue.GetEntryAsync(tokens[0]);
        QueueEntry? black = await queue.GetEntryAsync(tokens[1]);

        if (white is null || black is null)
        {
            logger.LogWarning(
                "Queue entry missing during matching — tokens: {White}, {Black}",
                tokens[0],
                tokens[1]);
            return;
        }

        try
        {
            var request = new CreateMatchRequest
            {
                White = new Player { UserId = white.UserId },
                Black = new Player { UserId = black.UserId },
                TimeControl = MapTimeControl(timeControl),
            };

            CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);

            string matchId = response.Match.Id;
            await queue.MarkMatchedAsync(tokens[0], white.UserId, matchId);
            await queue.MarkMatchedAsync(tokens[1], black.UserId, matchId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to create match for tokens {White} and {Black}", tokens[0], tokens[1]);
        }
    }
}
