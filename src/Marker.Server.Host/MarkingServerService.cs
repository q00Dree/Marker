using Marker.Server.Networking;

namespace Marker.Server.Host;

internal sealed class MarkingServerService(IMarkingServer server, ILogger<MarkingServerService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        server.ConnectionFaulted += ex => logger.LogError(ex, "Connection faulted.");
        server.Start();
        logger.LogInformation("Server started.");

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await server.DisposeAsync();
        logger.LogInformation("Server stopped.");
    }
}