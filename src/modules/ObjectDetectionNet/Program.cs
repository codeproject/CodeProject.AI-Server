using CodeProject.AI.Modules.ObjectDetection.Yolo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Can no longer be injected due to change in constructor signature
        // services.AddSingleton<ObjectDetector>();
        services.AddHostedService<ObjectDetectionWorker>();
    })
    .Build();

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
await host.RunAsync();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task