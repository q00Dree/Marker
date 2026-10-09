using Marker.Server.Networking;
using System.Net;

var builder = new MarkingServerBuilder()
    .Configure(o =>
    {
        o.Address = IPAddress.Any;
        o.Port = 5000;
        o.GenerationDelay = TimeSpan.FromMilliseconds(500);
    })
    .UseLineProtocol()
    .UseGs1Generator()
    .OnConnectionFaulted(ex => Console.Error.WriteLine($"Connection faulted: {ex}"));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

await using var server = builder.Build();
server.Start();

try
{
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (OperationCanceledException) { }