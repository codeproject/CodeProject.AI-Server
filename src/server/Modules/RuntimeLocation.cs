namespace CodeProject.AI.Server
{
    /// <summary>
    /// The location of the runtime to be used to launch a module.
    /// </summary>
    public enum RuntimeLocation
    {
        /// <summary>
        /// Unknown location. Here for completeness.
        /// </summary>
        Unknown,

        /// <summary>
        /// The runtime is installed locally within a module itself.
        /// </summary>
        Local,

        /// <summary>
        /// The runtime is installed in a shared location, typically /src/runtimes or app/runtimes.
        /// Folder name is given by AppSettings.RuntimesDirPath.
        /// </summary>
        Shared,

        /// <summary>
        /// The runtime is installed at the system level.
        /// </summary>
        System
    }
}
