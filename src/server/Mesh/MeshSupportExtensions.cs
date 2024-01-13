using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Sets up the Dependency Injection for Mesh objects and services
    /// </summary>
    public static class MeshSupportExtensions
    {
        /// <summary>
        /// Adds the mesh processing services
        /// </summary>
        /// <param name="services">The IServiceCollection service object</param>
        /// <param name="configuration">The IConfiguration object</param>
        /// <returns>An IServiceCollection object</returns>
        public static IServiceCollection AddMeshSupport(this IServiceCollection services, 
                                                        IConfiguration configuration)
        {
            services.Configure<MeshOptions>(configuration.GetSection(nameof(MeshOptions)));

            services.AddSingleton<MeshMonitor>();
            services.AddSingleton<MeshServerBroadcastBuilder>();
            services.AddSingleton<MeshManager>();
            services.AddSingleton<ServerSettingsJsonWriter>();

            return services;
        }
    }
}
