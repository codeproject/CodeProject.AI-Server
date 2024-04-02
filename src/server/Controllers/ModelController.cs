using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.Server.Models;

namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// For managing the models for the modules that form part of the system.
    /// </summary>
    [Route("v1/model")]
    [ApiController]
    public class ModelController : ControllerBase
    {
        private readonly ModelDownloader  _modelDownloader;
        private readonly ILogger          _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="modelDownloader">The model downloader instance.</param>
        /// <param name="logger">The logger</param>
        public ModelController(ModelDownloader modelDownloader,
                               ILogger<LogController> logger)
        {
            _modelDownloader = modelDownloader;
            _logger          = logger;
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services. This may include
        /// modules that can't be downloaded. They will be marked appropriately.
        /// </summary>
        /// <returns>A ResponseBase object containing a list of <see cref="ModelDownload"/>s
        /// objects.</returns>
        [HttpGet("list/downloadable", Name = "ListDownloadableModels")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> ListDownloadableModels()
        {
            // Download models.json file from CodeProject.com
            ModelDownloadCollection downloadables = await _modelDownloader.GetDownloadableModelsAsync()
                                                                          .ConfigureAwait(false);
            // return list of models by moduleId
            return new ModelListDownloadableResponse
            {
                Models = downloadables
            };
        }

        /// <summary>
        /// Manages requests to install the given module.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="filename">The version of the module to install</param>
        /// <param name="installFolderName">The name of the directing in the module's directory where
        /// the model will be extracted</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <param name="verbosity">The amount of noise to output when installing</param>
        /// <returns>A Response Object.</returns>
        [HttpPost("download/{moduleId}/{filename}/{installFolderName}", Name = "Download model")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> DownloadModelAsync(string moduleId, string filename,
                                                             string installFolderName,
                                                             [FromQuery] bool noCache = false, 
                                                             [FromQuery] LogVerbosity verbosity = LogVerbosity.Quiet)
        {
            var downloadTask =_modelDownloader.DownloadModelAsync(moduleId, filename,
                                                                  installFolderName,
                                                                  noCache, verbosity);
            (bool success, string error) = await downloadTask.ConfigureAwait(false);

            return success? new ServerResponse() : new ServerErrorResponse(error);
        }

        /*
        /// <summary>
        /// Manages requests to uninstall the given module.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("install/{moduleId}/{filename}", Name = "Delete model")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> DeleteModelAsync(string moduleId, string filename)
        {
            (bool success, string error) = await _moduleInstaller.UninstallModuleAsync(moduleId)
                                                                 .ConfigureAwait(false);
            
            return success? new ServerResponse() : new ServerErrorResponse(error);
        }
        */
    }
}
