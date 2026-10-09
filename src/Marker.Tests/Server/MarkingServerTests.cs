using Marker.Common.Protocol;
using Marker.Common.Protocol.Line;
using Marker.Server.Generation;
using Marker.Server.Generation.Gs1;
using Marker.Server.Networking;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Marker.Tests.Server;

public class MarkingServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FastDelay = TimeSpan.FromMilliseconds(20);

    // Сервер и тестовый клиент говорят через один и тот же протокол, формат кадров тестам не известен.
    private static readonly IMarkingCodeProtocol Protocol = new LineMarkingCodeProtocol();

    private sealed class DelegateGenerator(Func<string> generate) : IMarkingCodeGenerator
    {
        public string Generate() => generate();
    }

    /// <summary>Возвращает CODE-1, CODE-2, ... (общий счётчик на все подключения).</summary>
    private static IMarkingCodeGenerator Sequence()
    {
        var counter = 0;
        return new DelegateGenerator(() => $"CODE-{Interlocked.Increment(ref counter)}");
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static MarkingServerOptions OptionsOn(int port, TimeSpan? generationDelay = null) => new()
    {
        Address = IPAddress.Loopback,
        Port = port,
        GenerationDelay = generationDelay ?? FastDelay
    };

    private static MarkingServer ServerFor(
        MarkingServerOptions options, IMarkingCodeGenerator? generator = null) =>
        new(Options.Create(options), Protocol, generator ?? Sequence());

    private static async Task<(TcpClient Tcp, IMarkingCodeReader Reader)> ConnectAsync(int port)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(Timeout);
        return (tcp, Protocol.CreateReader(tcp.GetStream()));
    }

    private static Task<string?> NextAsync(IMarkingCodeReader reader) =>
        reader.ReadAsync(CancellationToken.None).WaitAsync(Timeout);

    [Fact]
    public async Task Start_ClientConnects_ReceivesGeneratedCodesInOrder()
    {
        // Arrange
        var port = GetFreePort();
        await using var server = ServerFor(OptionsOn(port));
        server.Start();

        // Act
        var (tcp, reader) = await ConnectAsync(port);
        using var _ = tcp;
        await using var __ = reader;

        // Assert
        Assert.Equal("CODE-1", await NextAsync(reader));
        Assert.Equal("CODE-2", await NextAsync(reader));
        Assert.Equal("CODE-3", await NextAsync(reader));
    }

    [Fact]
    public async Task Start_WithRealGs1Generator_CodesArriveIntact()
    {
        // Arrange
        const int count = 20;
        var generator = new Gs1MarkingCodeGenerator();
        var expectedLength = generator.Generate().Length;
        var port = GetFreePort();
        await using var server = ServerFor(OptionsOn(port), generator);
        server.Start();

        // Act
        var (tcp, reader) = await ConnectAsync(port);
        using var _ = tcp;
        await using var __ = reader;

        var received = new List<string>();
        for (var i = 0; i < count; i++)
            received.Add((await NextAsync(reader))!);

        // Assert: протокол не искажает и не режет настоящие коды
        Assert.Equal(count, received.Count);
        Assert.All(received, code =>
        {
            Assert.Equal(expectedLength, code.Length);
            Assert.StartsWith("01", code);
            Assert.DoesNotContain(code, char.IsWhiteSpace);
        });
    }

    [Fact]
    public async Task Start_ClientConnects_FirstCodeIsSentWithoutWaitingForDelay()
    {
        // Arrange
        var port = GetFreePort();
        await using var server = ServerFor(OptionsOn(port, generationDelay: TimeSpan.FromMinutes(1)));
        server.Start();

        // Act
        var (tcp, reader) = await ConnectAsync(port);
        using var _ = tcp;
        await using var __ = reader;

        // Assert
        Assert.Equal("CODE-1", await NextAsync(reader));
    }

    [Fact]
    public async Task Start_SeveralClients_EachGetsItsOwnStreamOfCodes()
    {
        // Arrange
        const int clients = 3;
        const int codesPerClient = 2;
        var port = GetFreePort();
        await using var server = ServerFor(OptionsOn(port));
        server.Start();

        // Act
        var received = new List<string>();
        for (var i = 0; i < clients; i++)
        {
            var (tcp, reader) = await ConnectAsync(port);
            using var _ = tcp;
            await using var __ = reader;

            for (var j = 0; j < codesPerClient; j++)
                received.Add((await NextAsync(reader))!);
        }

        // Assert: каждый клиент получил свои коды, генератор не выдал один код дважды
        Assert.Equal(clients * codesPerClient, received.Count);
        Assert.Equal(received.Count, received.Distinct().Count());
        Assert.All(received, code => Assert.StartsWith("CODE-", code));
    }

    [Fact]
    public async Task Start_ClientDisconnects_ServerStaysHealthyForOthers()
    {
        // Arrange
        var port = GetFreePort();
        var faults = new List<Exception>();
        await using var server = ServerFor(OptionsOn(port));
        server.ConnectionFaulted += faults.Add;
        server.Start();

        var (leavingTcp, leavingReader) = await ConnectAsync(port);
        await NextAsync(leavingReader);

        // Act: клиент уходит, сервер успевает несколько раз попытаться записать в закрытое соединение
        await leavingReader.DisposeAsync();
        leavingTcp.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        var (tcp, reader) = await ConnectAsync(port);
        using var _ = tcp;
        await using var __ = reader;

        // Assert
        Assert.NotNull(await NextAsync(reader));
        Assert.Empty(faults);
    }

    [Fact]
    public async Task Start_GeneratorThrows_RaisesConnectionFaultedAndKeepsAccepting()
    {
        // Arrange
        var port = GetFreePort();
        var faulted = new TaskCompletionSource<Exception>();
        var calls = 0;
        var generator = new DelegateGenerator(() =>
            Interlocked.Increment(ref calls) == 1
                ? throw new InvalidOperationException("boom")
                : "OK");
        await using var server = ServerFor(OptionsOn(port), generator);
        server.ConnectionFaulted += ex => faulted.TrySetResult(ex);
        server.Start();

        // Act
        var (brokenTcp, brokenReader) = await ConnectAsync(port);
        using var _ = brokenTcp;
        await using var __ = brokenReader;
        var brokenConnectionCode = await NextAsync(brokenReader);

        var (tcp, reader) = await ConnectAsync(port);
        using var ___ = tcp;
        await using var ____ = reader;

        // Assert
        var exception = await faulted.Task.WaitAsync(Timeout);
        Assert.Equal("boom", exception.Message);
        Assert.Null(brokenConnectionCode);
        Assert.Equal("OK", await NextAsync(reader));
    }

    [Fact]
    public async Task DisposeAsync_WithConnectedClient_ClosesConnectionAndStopsListening()
    {
        // Arrange
        var port = GetFreePort();
        var server = ServerFor(OptionsOn(port));
        server.Start();

        var (tcp, reader) = await ConnectAsync(port);
        using var _ = tcp;
        await using var __ = reader;
        await NextAsync(reader);

        // Act
        await server.DisposeAsync().AsTask().WaitAsync(Timeout);

        // Assert: клиент дочитывает уже отправленное и видит конец потока
        while (await NextAsync(reader) is not null) { }
        await Assert.ThrowsAsync<SocketException>(() => ConnectAsync(port));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var server = ServerFor(OptionsOn(GetFreePort()));
        server.Start();

        // Act & Assert
        await server.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var server = ServerFor(OptionsOn(GetFreePort()));

        // Act & Assert
        await server.DisposeAsync();
    }

    [Fact]
    public async Task Start_CalledTwice_Throws()
    {
        // Arrange
        await using var server = ServerFor(OptionsOn(GetFreePort()));
        server.Start();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(server.Start);
    }

    [Fact]
    public async Task Start_AfterDispose_ThrowsObjectDisposed()
    {
        // Arrange
        var server = ServerFor(OptionsOn(GetFreePort()));
        await server.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(server.Start);
    }

    [Fact]
    public void Constructor_AddressNotSet_Throws()
    {
        // Arrange
        var options = OptionsOn(GetFreePort());
        options.Address = null!;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ServerFor(options));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_PortOutOfRange_Throws(int port)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ServerFor(OptionsOn(port)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_NonPositiveGenerationDelay_Throws(int milliseconds)
    {
        // Arrange
        var options = OptionsOn(GetFreePort(), TimeSpan.FromMilliseconds(milliseconds));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ServerFor(options));
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        // Arrange
        var options = Options.Create(OptionsOn(GetFreePort()));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MarkingServer(null!, Protocol, Sequence()));
        Assert.Throws<ArgumentNullException>(() => new MarkingServer(options, null!, Sequence()));
        Assert.Throws<ArgumentNullException>(() => new MarkingServer(options, Protocol, null!));
    }
}
