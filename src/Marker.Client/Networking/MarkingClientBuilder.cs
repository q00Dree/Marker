using Marker.Common.Protocol;
using Marker.Common.Protocol.Line;

namespace Marker.Client.Networking;

public sealed class MarkingClientBuilder
{
    private MarkingClientOptions _options = new();
    private IMarkingCodeProtocol? _protocol;
    private Action? _onConnected;
    private Action<Exception?>? _onDisconnected;
    private Action<Exception>? _onConnectFailed;
    private Action? _onRetriesExhausted;

    public MarkingClientBuilder WithOptions(MarkingClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    public MarkingClientBuilder Configure(Action<MarkingClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    public MarkingClientBuilder UseLineProtocol() => UseProtocol(new LineMarkingCodeProtocol());
    public MarkingClientBuilder UseProtocol(IMarkingCodeProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
        return this;
    }

    public MarkingClientBuilder OnConnected(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _onConnected += handler;
        return this;
    }

    public MarkingClientBuilder OnDisconnected(Action<Exception?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _onDisconnected += handler;
        return this;
    }

    public MarkingClientBuilder OnConnectFailed(Action<Exception> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _onConnectFailed += handler;
        return this;
    }

    public MarkingClientBuilder OnRetriesExhausted(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _onRetriesExhausted += handler;
        return this;
    }

    public IMarkingClient Build()
    {
        if (_protocol is null)
            throw new InvalidOperationException("Protocol is not set. Call UseProtocol().");
        _options.Validate();

        var client = new MarkingClient(_options, _protocol);
        if (_onConnected is not null) client.Connected += _onConnected;
        if (_onDisconnected is not null) client.Disconnected += _onDisconnected;
        if (_onConnectFailed is not null) client.ConnectFailed += _onConnectFailed;
        if (_onRetriesExhausted is not null) client.RetriesExhausted += _onRetriesExhausted;

        return client;
    }
}
