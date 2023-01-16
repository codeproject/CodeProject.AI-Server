using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.AnalysisLayer.PortraitFilter;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<PortraitFilterWorker>();
    })
    .Build();

await host.RunAsync();
