using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using CodeProject.AI.Server.Modules;
using CodeProject.AI.Server.Mesh;

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
        /// <param name="configuration">The IConfiguration object</param>
        /// <returns>An IServiceCollection object</returns>
        public static IServiceCollection AddQueueProcessing(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<QueueProcessingOptions>(configuration.GetSection(nameof(QueueProcessingOptions)));

            services.AddSingleton<QueueServices>();
            services.AddSingleton<CommandDispatcher>();
            services.AddSingleton<BackendRouteMap>();
            services.AddSingleton<TriggerTaskRunner>();

            return services;
        }
    }
}
