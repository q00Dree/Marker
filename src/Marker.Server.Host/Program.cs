using Marker.Common.Protocol;
using Marker.Common.Protocol.Line;
using Marker.Server.Generation;
using Marker.Server.Generation.Gs1;
using Marker.Server.Host;
using Marker.Server.Networking;
using System.Net;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var serverSection = builder.Configuration.GetSection("Server");
builder.Services.AddOptions<MarkingServerOptions>()
    .Bind(serverSection)
    .Configure(o => o.Address = IPAddress.TryParse(serverSection["Address"], out var address)
        ? address
        : throw new InvalidOperationException("Server:Address is missing or is not a valid IP address."));

builder.Services.AddSingleton<IMarkingCodeProtocol, LineMarkingCodeProtocol>();
builder.Services.AddSingleton<IMarkingCodeGenerator>(_ => new Gs1MarkingCodeGenerator());
builder.Services.AddSingleton<IMarkingServer, MarkingServer>();

builder.Services.AddHostedService<MarkingServerService>();

await builder.Build().RunAsync();
