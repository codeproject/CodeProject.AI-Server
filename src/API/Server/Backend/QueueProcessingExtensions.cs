using CodeProject.AI.Server.Backend;

using Microsoft.Extensions.DependencyInjection;

namespace CodeProject.AI.API.Server.Backend
{
    public static class QueueProcessingExtensions
    {
        public static IServiceCollection AddQueueProcessing(this IServiceCollection services)
        {
            services.AddSingleton<QueueServices>();
            services.AddSingleton<CommandDispatcher>();
            services.AddSingleton<BackendRouteMap>();
            return services;
        }
    }
}
