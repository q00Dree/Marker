using Marker.Client.Networking;
using Marker.Client.Output;

namespace Marker.Client.Host;

internal sealed class MarkingClientService(
    IMarkingClient client,
    FileMarkingCodeOutput output,
    ILogger<MarkingClientService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        client.Connected += () => logger.LogInformation("Connected.");
        client.Disconnected += ex => logger.LogWarning("Disconnected{Reason}.", ex is null ? "" : $": {ex.Message}");
        client.ConnectFailed += ex => logger.LogError("Connect failed: {Message}", ex.Message);
        client.RetriesExhausted += () => logger.LogError("Retries exhausted.");

        client.Start();

        try
        {
            await foreach (var code in client.Codes.ReadAllAsync(stoppingToken))
                await output.WriteAsync(code, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}