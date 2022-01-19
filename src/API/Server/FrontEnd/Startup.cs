using System;
using System.IO;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using CodeProject.SenseAI.API.Common;
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
        /// This method gets called by the runtime. Use this method to add services to the container.
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

            services.Configure<VersionInfo>(Configuration.GetSection(nameof(VersionInfo)));

            services.Configure<InstallConfig>(Configuration.GetSection(InstallConfig.InstallCfgSection));

            services.AddBackendProcessRunner(Configuration);

            services.AddScoped<VersionService, VersionService>();
        }

        /// <summary>
        /// Configures the application pipeline.
        /// </summary>
        /// <param name="app">The Application Builder.</param>
        /// <param name="env">The Hosting Evironment.</param>
        /// <param name="logger">The logger</param>
        /// <param name="installConfig">The installation instance config values.</param>
        /// <remarks>
        ///   This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </remarks>
        public void Configure(IApplicationBuilder app,
                              IWebHostEnvironment env,
                              ILogger<Startup> logger,
                              IOptions<InstallConfig> installConfig)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeProject SenseAI API v1"));
            }

            InitializeIntallConfig(installConfig.Value, logger);

            bool forceHttps = Configuration.GetValue<bool>(nameof(forceHttps));
            if (forceHttps)
                app.UseHttpsRedirection();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors("allowAllOrigins");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static void InitializeIntallConfig(InstallConfig installConfig, ILogger<Startup> logger)
        {

            if (installConfig is null || installConfig.Id == Guid.Empty)
            {
                try
                {
                    installConfig  ??= new InstallConfig();
                    installConfig.Id = Guid.NewGuid();
                    var configValues = new { install = installConfig };
                    var filePath     = InstallConfig.InstallCfgFilename;
                    File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(configValues, 
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Exception updating {InstallConfig.InstallCfgFilename}");
                }
            }
        }
    }
}
