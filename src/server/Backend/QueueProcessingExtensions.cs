using Microsoft.Extensions.DependencyInjection;

using CodeProject.AI.Server.Modules;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// Manages the queue
    /// </summary>
    public static class QueueProcessingExtensions
    {
        /// <summary>
        /// Adds the queue processing services
        /// </summary>
        /// <param name="services">The IServiceCollection service object</param>
        /// <returns>An IServiceCollection object</returns>
        public static IServiceCollection AddQueueProcessing(this IServiceCollection services)
        {
            services.AddSingleton<QueueServices>();
            services.AddSingleton<CommandDispatcher>();
            services.AddSingleton<BackendRouteMap>();
            services.AddSingleton<TriggerTaskRunner>();

            return services;
        }
    }
}
