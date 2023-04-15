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

await host.RunAsync();