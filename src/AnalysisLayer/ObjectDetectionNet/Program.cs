using CodeProject.AI.AnalysisLayer.ObjectDetection.Yolo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ObjectDetector>();
        services.AddHostedService<ObjectDetectionWorker>();
    })
    .Build();

await host.RunAsync();