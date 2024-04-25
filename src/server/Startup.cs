using System;
using System.Collections.Generic;
using System.IO;

/*
#if Windows
using System.Security.Principal;
using System.Security.AccessControl;
#else
using System.Diagnostics;
#endif
*/

#if DEBUG
using System.Reflection;
using Microsoft.OpenApi.Models;
#endif

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.Server.Modules;
using CodeProject.AI.Server.Backend;
using CodeProject.AI.Server.Mesh;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// The Startup class
    /// </summary>
    public class Startup
    {
        private InstallConfig? _installConfig;
        private VersionConfig? _versionConfig;
        private ILogger<Startup>? _logger;

        /// <summary>
        /// Initializes a new instance of the Startup class.
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

#if DEBUG
            // http://localhost:32168/swagger/index.html
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
            // Make some hack corrections if needed
            LegacyParams.PassThroughLegacyCommandLineParams(Configuration);
            // ListConfigValues();

            // Configure application services and DI
            services.AddQueueProcessing(Configuration);

            services.AddModuleSupport(Configuration);

            services.AddVersionProcessRunner(Configuration);

            services.AddMeshSupport(Configuration);

            services.Configure<TriggersConfig>(Configuration.GetSection(TriggersConfig.TriggersCfgSection));

            // Configure the shutdown timeout to 60s instead of 2
            services.Configure<HostOptions>(
                opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Configures the application pipeline.
        /// </summary>
        /// <param name="app">The Application Builder.</param>
        /// <param name="env">The Hosting Environment.</param>
        /// <param name="logger">The logger</param>
        /// <param name="installConfig">The installation instance config values.</param>
        /// <param name="versionConfig">The Version Configuration</param>
        /// <remarks>
        ///   This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </remarks>
        public void Configure(IApplicationBuilder app,
                              IWebHostEnvironment env,
                              ILogger<Startup> logger,
                              IOptions<InstallConfig> installConfig,
                              IOptions<VersionConfig> versionConfig)
        {
            _installConfig = installConfig.Value;
            _versionConfig = versionConfig.Value;
            _logger        = logger;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if DEBUG                
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

            /* Should we choose to provide a folder in which we can dump items such as items
               generated by a module, we could do:
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders
                                            .PhysicalFileProvider($"{module.ModuleDirPath}/models");
            });

            that is, if you can decipher
            https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-7.0#serve-files-from-multiple-locations
            */

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
            // If the install config does not exist or has no ID, then we need to set it.
            if (_installConfig is null || _installConfig.Id == Guid.Empty)
            {
                _installConfig ??= new InstallConfig();
                _installConfig.Id = Guid.NewGuid();
            }

            // if this is a new install or replacing a pre V2.1 version
            // or there is an install file, then we need to install the initial modules.
            if (string.IsNullOrEmpty(_installConfig.Version) ||
                File.Exists(ModuleInstaller.InstallModulesFileName))
            {
                ModuleInstaller.QueueInitialModulesInstallation();
            }

            _installConfig.Version = _versionConfig?.VersionInfo?.Version ?? string.Empty;

            var configValues      = new { install = _installConfig };
            string appDataDir     = Configuration["ApplicationDataDir"] 
                                  ?? throw new ArgumentNullException("ApplicationDataDir is not defined in configuration");
            string configFilePath = Path.Combine(appDataDir, InstallConfig.InstallCfgFilename);

            try
            {
                if (!Directory.Exists(appDataDir))
                    Directory.CreateDirectory(appDataDir);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Unable to create application data folder: {ex.Message}");
            }

            if (Directory.Exists(appDataDir))
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string configJson = System.Text.Json.JsonSerializer.Serialize(configValues, options);
                try
                {
                    if (File.Exists(configFilePath))
                        File.SetAttributes(configFilePath, FileAttributes.Normal);
                    File.WriteAllText(configFilePath, configJson);

                    // Make sure everyone can get access to this
                    // SetWorldAccessToFile(configFilePath);
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
            foreach (KeyValuePair<string, string?> pair in Configuration.AsEnumerable())
            {
                Console.WriteLine($"{pair.Key}: {pair.Value ?? "<null>"}");
            }
        }

        /*
        /// <summary>
        /// Sets permissions on a file
        /// </summary>
        /// <param name="filePath">The path to the file you want to grant access to.</param>
        private void SetWorldAccessToFile(string filePath)
        {
            try
            {
#if Windows
                // var account = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                var account = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                FileSystemAccessRule rule = new (account, 
                                                FileSystemRights.ReadData | FileSystemRights.WriteData,
                                                AccessControlType.Allow);
                var file = new FileInfo(filePath);
                FileSecurity fileSecurity = file.GetAccessControl();
                fileSecurity.AddAccessRule(rule);
                file.SetAccessControl(fileSecurity);
#else
                // VSCode simply ignores defines sometimes
                if (!SystemInfo.IsWindows)
                {
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName               = "/bin/bash",
                            Arguments              = $"-c \"chmod o+rw '{filePath}'\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            UseShellExecute        = false,
                            CreateNoWindow         = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
#endif
                Console.WriteLine("File access granted to all users.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        */
    }
}
