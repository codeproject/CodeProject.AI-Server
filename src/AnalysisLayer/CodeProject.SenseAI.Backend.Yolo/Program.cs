using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Threading.Tasks;

namespace CodeProject.SenseAI.Analysis.Yolo
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<ObjectDetector>();
                    services.AddHostedService<YoloProcessor>();
                });
    }
}
