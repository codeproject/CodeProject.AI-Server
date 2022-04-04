using System;
using System.IO;
#if Windows
using System.Reflection;
#endif

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
#if Windows
using Microsoft.OpenApi.Models;
#endif

using CodeProject.SenseAI.API.Server.Backend;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Startup class
    /// </summary>
    public class Startup
    {
        private InstallConfig? _installConfig;
        private ILogger<Startup>? _logger;

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

#if Windows
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
                        Name = "Use under CPOL",
                        Url  = new Uri("https://www.codeproject.com/info/cpol10.aspx"),
                    }
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
#endif
            // ListConfigValues();

            // Configure application services and DI
            services.Configure<BackendOptions>(Configuration.GetSection(nameof(BackendOptions)))
                    .AddQueueProcessing();

            // Moved into its own file
            // services.Configure<VersionInfo>(Configuration.GetSection(nameof(VersionInfo)));

            services.Configure<InstallConfig>(Configuration.GetSection(InstallConfig.InstallCfgSection));
            services.Configure<VersionConfig>(Configuration.GetSection(VersionConfig.VersionCfgSection));

            services.AddBackendProcessRunner(Configuration);

            services.AddSingleton<VersionService, VersionService>();
            services.AddVersionProcessRunner();
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
            _installConfig = installConfig.Value;
            _logger        = logger;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if Windows                
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeProject SenseAI API v1"));
#endif
            }

            InitializeInstallConfig();

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

        private void InitializeInstallConfig()
        {
            if (_installConfig is null || _installConfig.Id == Guid.Empty)
            {
                try
                {
                    _installConfig  ??= new InstallConfig();
                    _installConfig.Id = Guid.NewGuid();

                    var configValues = new { install = _installConfig };

                    string appDataDir     = Configuration["ApplicationDataDir"];
                    string configFilePath = Path.Combine(appDataDir, InstallConfig.InstallCfgFilename);

                    if (!Directory.Exists(appDataDir))
                        Directory.CreateDirectory(appDataDir);

                    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    string configJson = System.Text.Json.JsonSerializer.Serialize(configValues, options);

                    File.WriteAllText(configFilePath, configJson);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Exception updating Install Config: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Lists the values from the combined configuration sources
        /// </summary>
        public void ListConfigValues()
        {
            foreach (KeyValuePair<string, string> pair in Configuration.AsEnumerable())
            {
                Console.WriteLine($"{pair.Key}: {pair.Value}");
            }
        }
    }
}
