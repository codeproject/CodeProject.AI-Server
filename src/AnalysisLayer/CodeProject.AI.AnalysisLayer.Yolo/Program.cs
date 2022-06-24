using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Threading.Tasks;

namespace CodeProject.AI.Analysis.Yolo
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // allow the app to be installed as a Windows Service or Linux systemd
                .UseWindowsService()
                .UseSystemd()

                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<ObjectDetector>();
                    services.AddHostedService<YoloProcessor>();
                });
    }
}
