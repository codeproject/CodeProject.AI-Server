using System.Text;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Represents a model that can be downloaded to a module's folder for use by the module
    /// </summary>
    public class ModelConfig
    {
        /// <summary>
        /// Gets or sets the id the model (Not actually used at the moment)
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Gets or sets the name of the model
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets a description of the model
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the actual filename of the model
        /// </summary>
        public string? Filename { get; set; }

        /// <summary>
        /// Gets or sets the folder this model should live in inside the module's base directory
        /// </summary>
        public string? Folder { get; set; }
        
        /// <summary>
        /// Gets or sets the size of the model in Kb
        /// </summary>
        public int FileSizeKb { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether this model should be downloaded at module
        /// install time.
        /// </summary>
        public bool PreInstall { get; set; } = false;
    }


    /// <summary>
    /// Represents the download information for a model
    /// </summary>
    public class ModelDownload : ModelConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not this model has been downloaded and the 
        /// models moved into place
        /// </summary>
        /// <remarks>
        /// When this (zip) file is downloaded and the files extracted to the correct folder, we
        /// currently have no way to associate those extracted files with this zip file. This means
        /// setting this "Downloaded" values is impossible after the fact, unless we introduce an
        /// array of filenames contained in this model download.
        /// </remarks>
        public bool Downloaded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this model has been downloaded and cached.
        /// This file will be cached in the downloads folder under "models".
        /// </summary>
        public bool Cached { get; set; }

        /// <summary>
        /// Creates a ModelDownload from a ModelConfig
        /// </summary>
        /// <param name="config">A model config entry</param>
        public static ModelDownload FromConfig(ModelConfig config)
        {
            return new ModelDownload() 
            {
                Name        = config.Name,
                Description = config.Description,
                Filename    = config.Filename,
                Folder      = config.Folder,
                FileSizeKb  = config.FileSizeKb,
                PreInstall  = config.PreInstall,
                Cached      = config.PreInstall     // This is a wild guess, but if it's preinstalled it's there, right?
            };
        }
    }
}