namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Where a given module lives
    /// </summary>
    public enum ModuleLocation
    {
        /// <summary>
        /// Unknown location. Here for completeness.
        /// </summary>
        Unknown,

        /// <summary>
        /// In the /src/modules or /app/modules folder. Folder name given by Appsettings.ModulesDirPath.
        /// </summary>
        Internal,

        /// <summary>
        /// In a folder external to the solution. Location given by the Appsettings.ExternalModulesDirPath.
        /// </summary>
        External,

        /// <summary>
        /// Modules that are preinstalled in Docker containers, in the /app/preinstalled-modules 
        /// folder. Folder name given by Appsettings.PreInstalledModulesDirPath.
        /// </summary>
        PreInstalled,

        /// <summary>
        /// Demo modules that live in /src/demos/modules. Folder name given by Appsettings.DemoModulesDirPath.
        /// </summary>
        Demos
    }
}
