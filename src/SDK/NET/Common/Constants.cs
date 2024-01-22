namespace CodeProject.AI.SDK.Common
{
    /// <summary>
    /// Contains constants for CodeProject.AI Server
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The name of the company publishing this (will first check project file - it should be there)
        /// </summary>
        public const string Company         = "CodeProject";

        /// <summary>
        /// The product name (will first check project file - it should be there)
        /// </summary>
        public const string ProductName     = "CodeProject.AI Server";

        /// <summary>
        /// The category of product. Handy for placing this in a subdir of the main company dir.
        /// </summary>
        public const string ProductCategory = "AI";

        /// <summary>
        /// The default port the server listens on using HTTP
        /// </summary>
        public const int DefaultPort    = 32168;

        /// <summary>
        /// The default port the server listens on when using HTTP with SSL/TLS
        /// </summary>
        public const int DefaultPortSsl = 32016;

        /// <summary>
        /// The legacy port (Deepstack compatibility) the server listens on
        /// </summary>
        public const int LegacyPort    = 5000;

        /// <summary>
        /// The legacy port the server listens on when running under macOS
        /// </summary>
        public const int LegacyPortOsx = 5500;

        /// <summary>
        /// Used wherever there needs to be a distinction between release and dev. Typically used in
        /// filenames
        /// </summary>
        public const string Development               = "development";

        /// <summary>
        /// The name of the file containing the settings for the server
        /// </summary>
        public const string ServerSettingsFilename    = "serversettings.json";

        /// <summary>
        /// The name of the file containing the settings for the server for the development environment
        /// </summary>
        public const string DevServerSettingsFilename = $"serversettings.{Development}.json";

        /// <summary>
        /// The name of the modules settings file without extension
        /// </summary>
        public const string ModulesSettingFilenameNoExt = "modulesettings";

        /// <summary>
        /// The name of the file containing the settings for a module, or a collection of modules
        /// (This file can handle one or more sets of settings)
        /// </summary>
        public const string ModuleSettingsFilename    = ModulesSettingFilenameNoExt + ".json";

        /// <summary>
        /// The name of the file containing the settings for a module, or a collection of modules,
        /// for the development environment
        /// </summary>
        public const string DevModuleSettingsFilename = $"{ModulesSettingFilenameNoExt}.{Development}.json";

        /// <summary>
        /// The name of the file that stores the list of known modules
        /// </summary>
        public const string ModulesListingFilename   = "modules.json";
    };
}