using System;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server
{
#pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    /// Represents a single log entry
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// The id of the log entry
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// The timestamp as UTC time
        /// </summary>
        public DateTime timestamp { get; set; }

        /// <summary>
        /// The log entry itself
        /// </summary>
        public string? entry { get; set; }

        /// <summary>
        /// The logging level of this entry
        /// </summary>
        public string? level { get; set; }

        /// <summary>
        /// The category for this entry, as defined in .NET logging
        /// </summary>
        public string? category { get; set; }

        /// <summary>
        /// The label for this entry. Can be anything such as 'timing', 'setup' etc. Something that
        /// provides context for whoever is consuming this entry.
        /// </summary>
        public string? label { get; set; }

        /// <summary>
        /// Gets or sets the exception info for this logging event, if any.
        /// </summary>
        public string? exception { get; set; }
    }

    /// <summary>
    /// The Response when requesting the list of log entries
    /// </summary>
    public class LogListResponse : ServerResponse
    {
        /// <summary>
        /// A list of log entries
        /// </summary>
        public LogEntry[]? Entries { get; set; }
    }
    #pragma warning restore IDE1006 // Naming Styles
}