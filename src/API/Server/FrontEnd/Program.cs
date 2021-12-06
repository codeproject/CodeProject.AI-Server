using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;


namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Application Entry Class.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The Application Entry Point.
        /// </summary>
        /// <param name="args">The command line args.</param>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Creates the Host Builder for the application
        /// </summary>
        /// <param name="args">The command line args</param>
        /// <returns>Returns the builder.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // configure for running as a Windows Service or LinuxSystemmd in addition as
                // an executable in either OS.
                .UseWindowsService()
                .UseSystemd()

                .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>();
                    })
            ;
    }
}


