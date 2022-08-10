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

using CodeProject.AI.API.Server.Backend;

namespace CodeProject.AI.API.Server.Frontend
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
                    Title          = "CodeProject.AI API",
                    Description    = "Provides a HTTP REST interface for the CodeProject.AI server.",
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
            PassThroughLegacyCommandLineParams();
            // ListConfigValues();

            // Configure application services and DI
            services.Configure<QueueProcessingOptions>(Configuration.GetSection(nameof(QueueProcessingOptions)))
                    .AddQueueProcessing();

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
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeProject.AI API v1"));
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
        /// Sniff the configuration values for root level values that we should pass on for
        /// backwards compatibility with legacy modules.
        /// 
        /// Here's how it works:
        /// 
        /// Command line values are available via the Configuration object.
        /// 
        /// Environment variables that an analysis module would typically access are set in 
        /// BackendProcessRunner.CreateProcessStartInfo. The values that CreateProcessStartInfo
        /// gets are from the server's appsettings.json in the EnvironmentVariables section, or
        /// from the backend analysis service's modulesettings.json file in its
        /// EnvironmentVariables section. These two sets of variables are combined into one set and
        /// then used to set the Environment variables for the backend analysis process being
        /// launched.
        /// 
        /// To override these values you just set the value of the environment variable using its
        /// name. The "name" is the tricky bit. In the appsettings.json file you have "CPAI_PORT"
        /// in the EnvironmentVariable section, but its fully qualified name is 
        /// FrontEndOptions:EnvironmentVariables:CPAI_PORT. In the custom object detection's variables
        /// in modulesettings.json, CPAI_PORT is Modules:CustomObjectDetection:EnvironmentVariables:CPAI_PORT.
        /// 
        /// So, to override these values at the command line is ludicrously verbose. Instead we
        /// will choose a subset of these variables that we know are in use in the wild and provide
        /// simple names that are mapped to the complicated names.
        /// 
        /// This is all horribly hardcoded, but the point here is that over time this list will
        /// disappear.
        /// </summary>
        public void PassThroughLegacyCommandLineParams()
        {
            var keyValues = new Dictionary<string, string>();

            // Go through the configuration looking for root level keys that could have been passed
            // via command line or otherwise. For the ones we find, convert them to value or values
            // that should be stored in configuration in a way that modules can access.
            foreach (KeyValuePair<string, string> pair in Configuration.AsEnumerable())
            {
                // Port. The big one
                if (pair.Key.Equals("PORT", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Front end (used by application clients)
                    keyValues["FrontEndOptions:EnvironmentVariables:CPAI_PORT"]               = pair.Value;
                    
                    // Back end (used by backend analysis modules)
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:CPAI_PORT"]        = pair.Value;
                    keyValues["Modules:ObjectDetection:EnvironmentVariables:CPAI_PORT"]       = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:CPAI_PORT"] = pair.Value;
                    keyValues["Modules:CustomObjectDetection:EnvironmentVariables:CPAI_PORT"] = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:CPAI_PORT"]   = pair.Value;
                    keyValues["Modules:PortraitFilter:EnvironmentVariables:CPAI_PORT"]        = pair.Value;
                }

                // Activation
                if (pair.Key.Equals("VISION-FACE", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_FACE", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:VISION-FACE"] = pair.Value;

                if (pair.Key.Equals("VISION-SCENE", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_SCENE", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:SceneClassification:EnvironmentVariables:VISION-SCENE"] = pair.Value;

                if (pair.Key.Equals("VISION-DETECTION", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_DETECTION", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:VISION-DETECTION"] = pair.Value;
                    keyValues["Modules:ObjectDetection:EnvironmentVariables:VISION-DETECTION"]       = pair.Value;
                }

                // Mode, which really comes down to resolution
                if (pair.Key.Equals("MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:MODE"]        = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:MODE"] = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:MODE"]   = pair.Value;
                    keyValues["Modules:ObjectDetection:EnvironmentVariables:MODE"]       = pair.Value;

                    switch (pair.Value.ToLower())
                    {
                        case "low": 
                            keyValues["Modules:CustomObjectDetection:EnvironmentVariables:RESOLUTION"] = "Small";
                            break;
                        case "medium":
                            keyValues["Modules:CustomObjectDetection:EnvironmentVariables:RESOLUTION"] = "medium";
                            break;
                        case "high":
                            keyValues["Modules:CustomObjectDetection:EnvironmentVariables:RESOLUTION"] = "high";
                            break;
                    }
                }

                // Using CUDA?
                if (pair.Key.Equals("CUDA_MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:CUDA_MODE"]        = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:CUDA_MODE"] = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:CUDA_MODE"]   = pair.Value;

                    keyValues["Modules:ObjectDetection:EnvironmentVariables:CUDA_MODE"]       = pair.Value;
                    keyValues["Modules:CustomObjectDetection:EnvironmentVariables:USE_CUDA"]  = pair.Value;
                }

                // Model Directories
                if (pair.Key.Equals("DATA_DIR", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:DATA_DIR"]        = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:DATA_DIR"] = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:DATA_DIR"]   = pair.Value;
                    keyValues["Modules:ObjectDetection:EnvironmentVariables:DATA_DIR"]       = pair.Value;
                }

                // Custom Model Directories. Deepstack compatibility and thge docs are ambiguous
                if (pair.Key.Equals("MODELSTORE-DETECTION", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:CustomObjectDetection:EnvironmentVariables:MODELS_DIR"] = pair.Value;

                // Temp Directories
                if (pair.Key.Equals("TEMP_PATH", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:TEMP_PATH"] = pair.Value;
            }

            // Now update the Configuration
            foreach (var pair in keyValues)
                Configuration[pair.Key] = pair.Value;
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
