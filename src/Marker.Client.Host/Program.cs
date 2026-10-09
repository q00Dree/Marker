using Marker.Client.Host;
using Marker.Client.Networking;
using Marker.Client.Output;
using Marker.Common.Protocol;
using Marker.Common.Protocol.Line;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.Configure<MarkingClientOptions>(builder.Configuration.GetSection("Client"));
builder.Services.Configure<OutputOptions>(builder.Configuration.GetSection("Output"));

builder.Services.AddSingleton<IMarkingCodeProtocol, LineMarkingCodeProtocol>();
builder.Services.AddSingleton<IMarkingClient, MarkingClient>();
builder.Services.AddSingleton(sp =>
{
    var path = sp.GetRequiredService<IOptions<OutputOptions>>().Value.Path;
    return new FileMarkingCodeOutput(path ?? throw new InvalidOperationException("Output:Path is not set."));
});

builder.Services.AddHostedService<MarkingClientService>();

await builder.Build().RunAsync();
