using Microsoft.Extensions.DependencyInjection;

namespace CodeProject.SenseAI.API.Server.Backend
{
    public static class QueueProcessingExtensions
    {
        public static IServiceCollection AddQueueProcessing(this IServiceCollection services)
        {
            services.AddSingleton<QueueServices>();
            services.AddSingleton<CommandDispatcher>();
            return services;
        }
    }
}
