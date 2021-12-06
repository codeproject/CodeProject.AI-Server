using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using System;
using System.IO;
using System.Reflection;

using CodeProject.SenseAI.API.Server.Backend;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Startup class
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializs a new instance of the Startup class.
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the application Configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Configures the application Services in DI.
        /// </summary>
        /// <param name="services">The application service collection.</param>
        /// <remarks>
        ///   This method gets called by the runtime. Use this method to add services to the container.
        /// </remarks>
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(c =>
            {
                c.AddPolicy(name: "allowAllOrigins", b =>
                {
                    b.AllowAnyOrigin();
                });
            });

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version        = "v1",
                    Title          = "CodeProject SenseAI API",
                    Description    = "Provides a HTTP REST interface for the CodeProject SenseAI server.",
                    TermsOfService = new Uri("https://www.codeproject.com/info/TermsOfUse.aspx"),
                    Contact        = new OpenApiContact
                    {
                        Name  = "CodeProject",
                        Email = "webmaster@codeproject.com",
                        Url   = new Uri("https://www.codeproject.com"),
                    },

                    License = new OpenApiLicense
                    {
                        Name = "Use under COPL",
                        Url  = new Uri("https://www.codeproject.com/info/cpol10.aspx"),
                    }
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            // Configure application services and DI
            services.Configure<BackendOptions>(Configuration.GetSection(nameof(BackendOptions)))
                    .AddQueueProcessing();
        }

        /// <summary>
        /// Configures the application pipeline.
        /// </summary>
        /// <param name="app">The Application Builder.</param>
        /// <param name="env">The Hosting Evironment.</param>
        /// <param name="logger">The logger</param>
        /// <param name="commandDispatcher">The Command Dispatcher.  Used to create the known queues.</param>
        /// <remarks>
        ///   This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </remarks>
        public void Configure(IApplicationBuilder app, 
                              IWebHostEnvironment env, 
                              ILogger<Startup> logger,
                              CommandDispatcher commandDispatcher)
        {
            /* Quick check
            Console.WriteLine($"Framework   : {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"OS          : {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Arch        : {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"Rundtime Dir: {RuntimeEnvironment.GetRuntimeDirectory()}");
            */

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeProject SenseAI API v1"));
            }

            bool forceHttps = Configuration.GetValue<bool>(nameof(forceHttps));
            if (forceHttps)
                app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("allowAllOrigins");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // create the queue we know we are going to be using.
            commandDispatcher.CreateKnownQueues();

        }
    }
}
