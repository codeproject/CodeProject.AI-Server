
using System.Collections.Generic;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Options used by the FrontEnd.
    /// </summary>
    public class FrontendOptions
    {
        /// <summary>
        /// Gets or sets the Root Directory of the installation.
        /// </summary>
        public string? ROOT_PATH { get; set; }

        /* These are last resort for helping correct paths...
         
        /// <summary>
        /// Gets or sets the name of the API Directory of the installation. This is only used to
        /// dynamically work up the directory chain to find the API directory in Development mode
        /// </summary>
        public string? APICODE_DIRNAME { get; set; }

        /// <summary>
        /// Gets or sets the name of the Directory in which the API server will be installed in
        /// production. This is used to assess at runtime where the exe is in relation to the root
        /// path.
        /// </summary>
        public string? SERVEREXE_DIRNAME { get; set; }
        */

        /// <summary>
        /// Gets or sets the root directory that contains the pre-installed backend modules.
        /// </summary>
        public string? MODULES_PATH { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the downloaded / sideloaded modules.
        /// </summary>
        public string? DOWNLOADED_MODULES_PATH { get; set; }

        /// <summary>
        /// Gets or sets the base directory for the python interpreters.
        /// </summary>
        public string? PYTHON_BASEPATH { get; set; }

        /// <summary>
        /// Gets or sets the tamplated path to the Python interpreter. This path
        /// may include a %PYTHON_RUNTIME% marker which will need to be replaced.
        /// </summary>
        public string? PYTHON_PATH { get; set; }

        /// <summary>
        /// Gets or sets the environment variables, common to the CodeProject.AI Server ecosystem.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }
    }

    /// <summary>
    /// Extension methods for the ModuleConfig class
    /// </summary>
    public static class FrontendOptionsExtensions
    {
        /// <summary>
        /// Adds (and overrides if needed) the environment variables from the FrontendOptions into
        /// the /// given dictionary.
        /// </summary>
        /// <param name="frontend">This frontend object</param>
        /// <param name="environmentVars">The dictionary to which the vars will be added/updated</param>
        public static void AddEnvironmentVariables(this FrontendOptions frontend,
                                                   Dictionary<string, string?> environmentVars)
        {
            if (frontend.EnvironmentVariables is not null)
            {
                foreach (var entry in frontend.EnvironmentVariables)
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
