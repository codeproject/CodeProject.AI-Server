using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.SenseAI.API.Server.Backend;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend processes.
    /// </summary>
    public class BackendProcessRunner : BackgroundService
    {
        // marker for path substitution
        const string RootDirMarker         = "%ROOT_DIR%";
        const string AppRootMarker         = "%APP_ROOT%";
        const string PythonDirMarker       = "%PYTHON_DIR%";

        const string FaceEnableEnvVar      = "VISION-FACE";
        const string DetectionEnableEnvVar = "VISION-DETECTION";
        const string SceneEnableEnvVar     = "VISION-SCENE";
        const string AllEnableEnvVar       = "VISION-ALL";

        // the list of environment variables to pass to the backend processes.
        private readonly string[] BackendEnvironmentVarNames =
        {
            "APPDIR", "DATA_DIR", "TEMP_PATH", "MODELS_DIR", "PROFILE", "CUDA_MODE",
            FaceEnableEnvVar, DetectionEnableEnvVar, SceneEnableEnvVar,
            AllEnableEnvVar, "PORT"
        };

        private readonly FrontendOptions               _options;
        private readonly IConfiguration                _config;
        private readonly ILogger<BackendProcessRunner> _logger;
        private readonly QueueServices                 _queueServices;
        private readonly Dictionary<string, string?>   _backendEnvironmentVars = new();
        private readonly List<Process>                 _runningProcesses = new();

        /// <summary>
        /// Gets a list of the startup processes.
        /// </summary>
        public StartupProcess[] StartupProcesses
        {
            get { return _options?.StartupProcesses ?? Array.Empty<StartupProcess>(); }
        }

        /// <summary>
        /// Initialises a new instance of the BackendProcessRunner.
        /// </summary>
        /// <param name="options">The FrontendOptions</param>
        /// <param name="config">The application configuration.</param>
        /// <param name="queueServices">The Queue management service.</param>
        /// <param name="logger">The logger.</param>
        public BackendProcessRunner(IOptions<FrontendOptions> options,
                                    IConfiguration config,
                                    QueueServices queueServices,
                                    ILogger<BackendProcessRunner> logger)
        {
            _options = options.Value;
            _config = config;
            _logger = logger;
            _queueServices = queueServices;

            ExpandOptions();
            BuildBackendEnvironmentVar();
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            if (_options.StartupProcesses is not null)
            {
                foreach (var cmdInfo in _options.StartupProcesses)
                {
                    cmdInfo.Running = false;

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    var enabled = _config.GetValue<bool>(cmdInfo.EnableFlag);

                    if (enabled && !string.IsNullOrEmpty(cmdInfo.Command))
                    {
                        // _logger.LogError($"Starting {cmdInfo.Command}");

                        var procStartInfo = new ProcessStartInfo(cmdInfo.Command, cmdInfo.Args ?? "");
                        procStartInfo.UseShellExecute = false;

                        // setup the environment
                        foreach (var kv in _backendEnvironmentVars)
                            procStartInfo.Environment.TryAdd(kv.Key, kv.Value);

                        // create the required Queue
                        if (!string.IsNullOrWhiteSpace(cmdInfo.Queue))
                        {
                            foreach (var queueName in cmdInfo.Queue
                                .Split(',', StringSplitOptions.RemoveEmptyEntries
                                          | StringSplitOptions.TrimEntries))
                                _queueServices.EnsureQueueExists(queueName);
                        }

                        try
                        {
                            Process? process = Process.Start(procStartInfo);
                            if (process is not null)
                            {
                                _logger.LogInformation($"Started {cmdInfo.Name} backend");
                                _runningProcesses.Add(process);
                                cmdInfo.Running = true;
                            }
                            else
                            {
                                _logger.LogError($"Unable to start {cmdInfo.Name} backend");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error trying to start { cmdInfo.Name} backend");
                        }
                    }
                }

                await Task.Delay(-1, stoppingToken);

                foreach (var process in _runningProcesses)
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }

                foreach (var cmdInfo in _options.StartupProcesses)
                {
                    cmdInfo.Running = false;
                }
            }
        }

        /// <summary>
        /// Expands all the directory markers in the options.
        /// </summary>
        private void ExpandOptions()
        {
            if (_options is null)
                return;

            // These first three options need to be expanded first.
            // var rootDir = Path.Combine(AppContext.BaseDirectory, _options.ROOT_DIR ?? "");
            // _options.ROOT_DIR = new DirectoryInfo(rootDir).FullName;

            DirectoryInfo currentDir = new DirectoryInfo(AppContext.BaseDirectory);
            if (_options.API_DIRNAME != null)
            {
                // Find the API dir
                while (currentDir.Parent != null && currentDir.Name.ToLower() != _options.API_DIRNAME.ToLower())
                    currentDir = currentDir.Parent;

                // Up to the src dir
                if (currentDir != null && currentDir.Parent != null)
                    currentDir = currentDir.Parent;

                // Up to the root dir
                if (currentDir != null && currentDir.Parent != null)
                    currentDir = currentDir.Parent;
            }

            _options.ROOT_DIR = currentDir?.FullName ?? string.Empty;

            // _logger.LogError($"_options.ROOT_DIR: {_options.ROOT_DIR}");

            _options.APP_ROOT   = ExpandOption(_options.APP_ROOT);
            _options.PYTHON_DIR = ExpandOption(_options.PYTHON_DIR);

            if (_options.StartupProcesses is not null)
            {
                foreach (var backend in _options.StartupProcesses)
                {
                    backend.Command = ExpandOption(backend.Command);
                    backend.Args    = ExpandOption(backend.Args);
                }
            }
        }

        /// <summary>
        /// Expands the directory markers in the string.
        /// </summary>
        /// <param name="value">The value to expand.</param>
        /// <returns>The expanded path.</returns>
        private string? ExpandOption(string? value)
        {
            if (value is null)
                return null;

            value = value.Replace(PythonDirMarker, _options.PYTHON_DIR);
            value = value.Replace(AppRootMarker,   _options.APP_ROOT);
            value = value.Replace(RootDirMarker,   _options.ROOT_DIR);

            return value;
        }

        /// <summary>
        /// Creates the collection of backend environment variables.
        /// </summary>
        private void BuildBackendEnvironmentVar()
        {
            foreach (var varName in BackendEnvironmentVarNames)
            {
                var value = _config[varName];
               _backendEnvironmentVars.Add(varName, ExpandOption(value));
            }
        }
    }

    /// <summary>
    /// Extension methods for the BackendProcessRunner.
    /// </summary>
    public static class BackendProcessRunnerExtensions
    {
        /// <summary>
        /// Sets up the BackendProcessRunner.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <param name="configuration">The Configuration.</param>
        /// <returns></returns>
        public static IServiceCollection AddBackendProcessRunner(this IServiceCollection services,
                                                                 IConfiguration configuration)
        {
            services.Configure<FrontendOptions>(configuration.GetSection("FrontEndOptions"));
            services.AddHostedService<BackendProcessRunner>();
            return services;
        }
    }
}
