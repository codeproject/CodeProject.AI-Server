
using System;
using System.Collections.Generic;

using CodeProject.SenseAI.Server.Backend;

namespace CodeProject.SenseAI.API.Server.Frontend
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
        /// Gets or sets the root directory that contains the backend modules.
        /// </summary>
        public string? MODULES_PATH { get; set; }

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
        /// Gets or sets the environment variables, common to the SenseAI Server ecosystem, to set.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }
    }
}
