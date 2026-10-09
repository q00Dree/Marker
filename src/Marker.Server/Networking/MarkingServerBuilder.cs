using Marker.Common.Protocol;
using Marker.Common.Protocol.Line;
using Marker.Server.Generation;
using Marker.Server.Generation.Gs1;

namespace Marker.Server.Networking;

public sealed class MarkingServerBuilder
{
    private MarkingServerOptions _options = new();
    private IMarkingCodeProtocol? _protocol;
    private IMarkingCodeGenerator? _generator;
    private Action<Exception>? _onConnectionFaulted;

    public MarkingServerBuilder WithOptions(MarkingServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    public MarkingServerBuilder Configure(Action<MarkingServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    public MarkingServerBuilder UseLineProtocol() => UseProtocol(new LineMarkingCodeProtocol());
    public MarkingServerBuilder UseProtocol(IMarkingCodeProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
        return this;
    }

    public MarkingServerBuilder UseGs1Generator() => UseGenerator(new Gs1MarkingCodeGenerator());
    public MarkingServerBuilder UseGenerator(IMarkingCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        return this;
    }

    public MarkingServerBuilder OnConnectionFaulted(Action<Exception> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _onConnectionFaulted += handler;
        return this;
    }

    public IMarkingServer Build()
    {
        if (_protocol is null)
            throw new InvalidOperationException("Protocol is not set. Call UseProtocol().");
        if (_generator is null)
            throw new InvalidOperationException("Generator is not set. Call UseGenerator().");

        var server = new MarkingServer(_options, _protocol, _generator);
        if (_onConnectionFaulted is not null)
            server.ConnectionFaulted += _onConnectionFaulted;

        return server;
    }
}