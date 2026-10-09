using Marker.Client.Networking;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Marker.Tests.Client;

public class MarkingClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static void ConfigureFast(MarkingClientOptions o, int bufferCapacity = 16)
    {
        o.BufferCapacity = bufferCapacity;
        o.Resilience.InitialDelay = TimeSpan.FromMilliseconds(20);
        o.Resilience.MaxDelay = TimeSpan.FromMilliseconds(100);
        o.Resilience.Multiplier = 2;
    }

    private static TcpListener StartListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static MarkingClientBuilder ClientFor(TcpListener listener, int bufferCapacity = 16) =>
        new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                ConfigureFast(o, bufferCapacity);
            })
            .UseLineProtocol();

    private static async Task SendAndCloseAsync(TcpListener listener, string text)
    {
        using var peer = await listener.AcceptTcpClientAsync();
        await peer.GetStream().WriteAsync(System.Text.Encoding.UTF8.GetBytes(text));
    }

    private static Task<string> NextAsync(IMarkingClient client) =>
        client.Codes.ReadAsync().AsTask().WaitAsync(Timeout);

    [Fact]
    public async Task Start_ServerSendsCodes_ExposesThemInOrder()
    {
        // Arrange
        var listener = StartListener();
        await using var client = ClientFor(listener).Build();

        // Act
        client.Start();
        await SendAndCloseAsync(listener, "AAA\nBBB\n").WaitAsync(Timeout);

        // Assert
        Assert.Equal("AAA", await NextAsync(client));
        Assert.Equal("BBB", await NextAsync(client));
        listener.Stop();
    }

    [Fact]
    public async Task Start_ConnectionDropped_ReconnectsAndKeepsSameStream()
    {
        // Arrange
        var listener = StartListener();
        var disconnected = new TaskCompletionSource();
        await using var client = ClientFor(listener)
            .OnDisconnected(_ => disconnected.TrySetResult())
            .Build();

        // Act
        client.Start();
        await SendAndCloseAsync(listener, "AAA\n").WaitAsync(Timeout);
        await disconnected.Task.WaitAsync(Timeout);
        await SendAndCloseAsync(listener, "BBB\n").WaitAsync(Timeout);

        // Assert
        Assert.Equal("AAA", await NextAsync(client));
        Assert.Equal("BBB", await NextAsync(client));
        listener.Stop();
    }

    [Fact]
    public async Task Start_FullBuffer_SlowConsumerLosesNothing()
    {
        // Arrange
        var listener = StartListener();
        await using var client = ClientFor(listener, bufferCapacity: 1).Build();

        // Act
        client.Start();
        await SendAndCloseAsync(listener, "AAA\nBBB\nCCC\nDDD\n").WaitAsync(Timeout);
        await Task.Delay(100);

        // Assert
        foreach (var expected in new[] { "AAA", "BBB", "CCC", "DDD" })
            Assert.Equal(expected, await NextAsync(client));
        listener.Stop();
    }

    [Fact]
    public async Task Start_ServerSilent_DisconnectsWithTimeoutAndReconnects()
    {
        // Arrange
        var listener = StartListener();
        var disconnected = new TaskCompletionSource<Exception?>();
        await using var client = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                ConfigureFast(o);
                o.IdleTimeout = TimeSpan.FromMilliseconds(100);
            })
            .UseLineProtocol()
            .OnDisconnected(ex => disconnected.TrySetResult(ex))
            .Build();

        // Act: сервер принимает соединение, но молчит
        client.Start();
        using var silentPeer = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);

        // Assert: клиент рвёт соединение по таймауту и подключается снова
        Assert.IsType<TimeoutException>(await disconnected.Task.WaitAsync(Timeout));
        using var secondPeer = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);
        listener.Stop();
    }

    [Fact]
    public async Task Start_SlowConsumer_IsNotTreatedAsIdle()
    {
        // Arrange
        var listener = StartListener();
        var disconnected = false;
        await using var client = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                ConfigureFast(o, bufferCapacity: 1);
                o.IdleTimeout = TimeSpan.FromMilliseconds(100);
            })
            .UseLineProtocol()
            .OnDisconnected(_ => disconnected = true)
            .Build();

        // Act: буфер переполнен, клиент ждёт потребителя дольше таймаута
        client.Start();
        using var peer = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);
        await peer.GetStream().WriteAsync("AAA\nBBB\nCCC\n"u8.ToArray());
        await Task.Delay(300);

        // Assert
        Assert.False(disconnected);
        foreach (var expected in new[] { "AAA", "BBB", "CCC" })
            Assert.Equal(expected, await NextAsync(client));
        listener.Stop();
    }

    [Fact]
    public async Task Start_ServerUnavailable_RaisesConnectFailed()
    {
        // Arrange
        var listener = StartListener();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var failed = new TaskCompletionSource<Exception>();
        await using var client = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = port;
                ConfigureFast(o);
            })
            .UseLineProtocol()
            .OnConnectFailed(ex => failed.TrySetResult(ex))
            .Build();

        // Act
        client.Start();

        // Assert
        Assert.IsType<SocketException>(await failed.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Start_AttemptsExhausted_RaisesEventAndCompletesCodes()
    {
        // Arrange
        var listener = StartListener();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var failures = 0;
        var exhausted = new TaskCompletionSource();
        await using var client = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = port;
                ConfigureFast(o);
                o.Resilience.MaxAttempts = 0;
            })
            .UseLineProtocol()
            .OnConnectFailed(_ => Interlocked.Increment(ref failures))
            .OnRetriesExhausted(() => exhausted.TrySetResult())
            .Build();

        // Act
        client.Start();

        // Assert
        await exhausted.Task.WaitAsync(Timeout);
        await client.Codes.Completion.WaitAsync(Timeout);
        Assert.Equal(1, failures);
    }

    [Fact]
    public async Task DisposeAsync_WhileConnected_StopsCleanlyAndCompletesCodes()
    {
        // Arrange
        var listener = StartListener();
        var connected = new TaskCompletionSource();
        var client = ClientFor(listener)
            .OnConnected(() => connected.TrySetResult())
            .Build();
        client.Start();
        using var peer = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);
        await connected.Task.WaitAsync(Timeout);

        // Act
        await client.DisposeAsync().AsTask().WaitAsync(Timeout);

        // Assert
        await client.Codes.Completion.WaitAsync(Timeout);
        listener.Stop();
    }

    [Fact]
    public async Task DisposeAsync_BufferFullAndConsumerStuck_StopsCleanly()
    {
        // Arrange
        var listener = StartListener();
        var client = ClientFor(listener, bufferCapacity: 1).Build();
        client.Start();
        await SendAndCloseAsync(listener, "AAA\nBBB\nCCC\n").WaitAsync(Timeout);
        await Task.Delay(100);

        // Act & Assert
        await client.DisposeAsync().AsTask().WaitAsync(Timeout);
        listener.Stop();
    }

    [Fact]
    public void Build_WithoutHost_Throws()
    {
        // Arrange
        var builder = new MarkingClientBuilder().UseLineProtocol();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void Build_NonPositiveIdleTimeout_Throws()
    {
        // Arrange
        var builder = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = 5000;
                ConfigureFast(o);
                o.IdleTimeout = TimeSpan.Zero;
            })
            .UseLineProtocol();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void Build_ZeroBufferCapacity_Throws()
    {
        // Arrange
        var builder = new MarkingClientBuilder()
            .Configure(o =>
            {
                o.Host = "127.0.0.1";
                o.Port = 5000;
                ConfigureFast(o, bufferCapacity: 0);
            })
            .UseLineProtocol();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(builder.Build);
    }
}