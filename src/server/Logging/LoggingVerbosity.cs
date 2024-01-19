namespace CodeProject.AI.Server
{
    /// <summary>
    /// LogVerbosity
    /// </summary>
    public enum LogVerbosity
    {
        /// <summary>
        /// Unknown verbosity. Here for completeness.
        /// </summary>
        Unknown,

        /// <summary>
        /// Only the essentials.
        /// </summary>
        Quiet,

        /// <summary>
        /// Anything meaningful to the user without being noisy
        /// </summary>
        Info,

        /// <summary>
        /// Everything including the kitchen sink.
        /// </summary>
        Loud
    }
}
