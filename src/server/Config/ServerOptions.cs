
using System.Collections.Generic;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Options used by the server.
    /// </summary>
    public class ServerOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the server should run in offline mode and not
        /// make calls to get updates or module download listings.
        /// </summary>
        public bool? AllowInternetAccess { get; set; }

        /// <summary>
        /// The URL the server uses to check for the latest available server version.
        /// </summary>
        public string? ServerVersionCheckUrl { get; set; }

        /// <summary>
        /// The URL the server uses to download the latest updated version.
        /// </summary>
        public string? ServerDownloadUrl { get; set; }

        /// <summary>
        /// The URL the server uses to download the latest updated version.
        /// </summary>
        public string? ModuleDownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the environment variables, common to the CodeProject.AI Server ecosystem.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }
    }

    /// <summary>
    /// Extension methods for the ModuleConfig class
    /// </summary>
    public static class ServerOptionsExtensions
    {
        /// <summary>
        /// Adds (and overrides if needed) the environment variables from the ServerOptions into
        /// the given dictionary.
        /// </summary>
        /// <param name="serverOptions">This server options object</param>
        /// <param name="environmentVars">The dictionary to which the vars will be added/updated</param>
        public static void AddEnvironmentVariables(this ServerOptions serverOptions,
                                                   Dictionary<string, string?> environmentVars)
        {
            if (serverOptions.EnvironmentVariables is not null)
            {
                foreach (var entry in serverOptions.EnvironmentVariables)
                {
                    string key = entry.Key.ToUpper();
                    if (environmentVars.ContainsKey(key))
                        environmentVars[key] = entry.Value.ToString();
                    else
                        environmentVars.Add(key, entry.Value.ToString());
                }
            }
        }
    }
}
