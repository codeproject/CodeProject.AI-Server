
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.Modules.SentimentAnalysis;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<TextClassifier>();
        services.AddHostedService<SentimentAnalysisWorker>();
    })
    .Build();

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
await host.RunAsync();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
