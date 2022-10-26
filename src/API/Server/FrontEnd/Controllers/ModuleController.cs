using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.API.Common;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// For managing the optional modules that form part of the system.
    /// </summary>
    [Route("v1/module")]
    [ApiController]
    public class ModuleController : ControllerBase
    {
        private static HttpClient? _client;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly FrontendOptions _frontendOptions;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">The Configuration instance</param>
        /// <param name="options">The Frontend Options</param>
        /// <param name="logger">The logger</param>
        public ModuleController(IConfiguration configuration,
                                IOptions<FrontendOptions> options,
                                ILogger<LogController> logger)
        {
            _logger = logger;
            _configuration = configuration;
            _frontendOptions = options.Value;
        }


        /// <summary>
        /// Allows for a client to list the installed backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/installed", Name = "ListInstalledModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListInstalledModules()
        {
            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return new ErrorResponse("Unable to locate backend services");

            if (backend.ProcessStatuses is null)
                return new ErrorResponse("No backend processes have been registered");

            // List them out and return the status
            var response = new ModuleListResponse
            {
                modules = backend.Modules.Values.Select(module => new ModuleDescription()
                {
                    ModuleId  = module.ModuleId,
                    Name      = module.Name,
                    Platforms = module.Platforms,
                    Version   = module.Version
                }).ToList()
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to list of backend analysis services available for download
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/available", Name = "ListAvailableModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> ListAvailableModules()
        {
            List<ModuleDescription>? moduleList = null;

            if (_client is null)
                _client = new HttpClient { Timeout = new TimeSpan(0, 0, 30) };

            try
            {
                // string moduleListUrl = _configuration.GetValue<string>("ModuleListUrl");
                // string data = await _client.GetStringAsync(moduleListUrl);
                string? data = await System.IO.File.ReadAllTextAsync(_frontendOptions.ROOT_PATH! + "\\modules.json");

                if (!string.IsNullOrWhiteSpace(data))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    moduleList = JsonSerializer.Deserialize<List<ModuleDescription>>(data, options);
                    if (moduleList is not null)
                    {
                        // A small adjustment. The version info contains the file *name* not a file
                        // URL. We return just the name as a naive protection against man in the
                        // middle attacks. The actual URL we send the user to will come from the
                        // local config settings.
                        /*
                        if (!string.IsNullOrWhiteSpace(version.File))
                        {
                            string updateDownloadUrl = Configuration.GetValue<string>("UpdateDownloadUrl");
                            version.File = updateDownloadUrl;
                        }
                        */
                        // _logger.LogInformation($"Latest version available is {version.Version}");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error checking for available modules: " + e.Message);
            }

            var response = new ModuleListResponse()
            {
                modules = moduleList!
            };

            return response;
        }
    }
}
