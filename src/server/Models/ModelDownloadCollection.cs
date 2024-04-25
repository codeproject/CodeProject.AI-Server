using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server.Models
{
    /// <summary>
    /// The Response when requesting information on downloadable models
    /// /// </summary>
    public class ModelListDownloadableResponse: ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of model downloads
        /// </summary>
        public ModelDownloadCollection? Models { get; set; }
    }
    
    /// <summary>
    /// The set of models available for download for a given module.
    /// </summary>
    public class ModelDownloadCollection : ConcurrentDictionary<string, ModelDownload[]>
    {
        /// <summary>
        /// This constructor allows our models collection to be case insensitive on the key.
        /// </summary>
        public ModelDownloadCollection() : base(StringComparer.OrdinalIgnoreCase) { }

        /// <summary>
        /// Add an array of ModelConfig to the array of ModelDownloads for the given module Id 
        /// </summary>
        /// <param name="moduleId"></param>
        /// <param name="configs"></param>
        /// <returns></returns>
        public bool Merge(string moduleId, ModelConfig[]? configs)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || configs is null)
                return false;

            List<ModelDownload> models;
            if (ContainsKey(moduleId))
                models = this[moduleId].ToList();
            else
                models = new List<ModelDownload>(configs.Length);

            foreach (ModelConfig config in configs)
                if (!models.Any(m => m.Filename == config.Filename || m.Name == config.Name))
                    models.Add(ModelDownload.FromConfig(config));

            this[moduleId] = models.ToArray();

            return true;
        }

        /// <summary>
        /// Add an array of ModelDownloads to the array of ModelDownloads for the given module Id 
        /// </summary>
        /// <param name="moduleId">The module Id</param>
        /// <param name="downloads">The downloadable modules for the given module</param>
        /// <returns></returns>
        public bool Merge(string moduleId, ModelDownload[]? downloads)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || downloads is null)
                return false;

            if (!ContainsKey(moduleId))
            {
                this[moduleId] = downloads;
            }
            else
            {
                List<ModelDownload> models = this[moduleId].ToList();
                foreach (ModelDownload download in downloads)
                    if (!models.Any(m => m.Filename == download.Filename || m.Name == download.Name))
                        models.Add(download);

                this[moduleId] = models.ToArray();
            }

            return true;
        }
    }
}
