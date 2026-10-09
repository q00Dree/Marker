using Marker.Client.Networking;
using Marker.Client.Output;

await using var output = new FileMarkingCodeOutput("codes.txt");

var builder = new MarkingClientBuilder()
    .Configure(o =>
    {
        o.Host = "127.0.0.1";
        o.Port = 5000;
        o.BufferCapacity = 1000;
        o.Resilience.InitialDelay = TimeSpan.FromSeconds(1);
        o.Resilience.MaxDelay = TimeSpan.FromSeconds(30);
        o.Resilience.Multiplier = 2;
        o.Resilience.MaxAttempts = null;
    })
    .UseLineProtocol()
    .OnConnected(() => Console.WriteLine("Connected."))
    .OnDisconnected(ex => Console.WriteLine(ex is null ? "Disconnected." : $"Disconnected: {ex.Message}"))
    .OnRetriesExhausted(() => Console.Error.WriteLine("Retries exhausted."))
    .OnConnectFailed(ex => Console.Error.WriteLine($"Connect failed: {ex.Message}"));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

await using var client = builder.Build();
client.Start();

try
{
    await foreach (var code in client.Codes.ReadAllAsync(shutdown.Token))
        await output.WriteAsync(code, shutdown.Token);
}
catch (OperationCanceledException) { }