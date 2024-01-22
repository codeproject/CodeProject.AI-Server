using System.Text.Json.Serialization;

namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// The current version of the server.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Gets or sets the major version.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Gets or sets the minor version.
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Gets or sets the patch number
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the pre-release identifier.
        /// </summary>
        public string? PreRelease { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this version contains a security update
        /// </summary>
        public bool SecurityUpdate { get; set; }

        /// <summary>
        /// Gets or sets the build number
        /// </summary>
        public int Build { get; set; }

        /// <summary>
        /// Gets or sets the filename, in the official download directory, containing this version
        /// </summary>
        public string? File { get; set; }

        /// <summary>
        /// Gets or sets the main release notes for this version.
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// Gets a string representation of the version without pre-release or build info
        /// </summary>
        [JsonIgnore]
        public string VersionNumber
        {
            get
            {
                // https://semver.org/
                string version = $"{Major}.{Minor}";

                if (Patch > 0)
                    version += $".{Patch}";

                return version;
            }
        }

        /// <summary>
        /// Gets a string representation of the full version
        /// </summary>
        [JsonIgnore]
        public string Version
        {
            get
            {
                // https://semver.org/
                string version = VersionNumber;

                if (!string.IsNullOrWhiteSpace(PreRelease))
                    version += $"-{PreRelease}";

                if (Build > 0)
                    version += $"+{Build:0000}";

                return version;
            }
        }

        public override string ToString()
        {
            return Version;
        }

        public static VersionInfo Parse(string? versionString)
        {
            var version = new VersionInfo();
            if (string.IsNullOrEmpty(versionString))
                return version;

            // Extract and remove build
            string[] parts = versionString.Split('+');
            if (parts.Length > 1)
                version.Build = int.Parse(parts[1]);

            // Extract and remove PreRelease from LHS of Build split
            parts = parts[0].Split('-');
            if (parts.Length > 1)
                version.PreRelease = parts[1];

            // Extract versions from LHS of PreRelease split
            parts = parts[0].Split('.');

            if (parts.Length > 0)
                version.Major = int.Parse(parts[0]);
            if (parts.Length > 1)
                version.Minor = int.Parse(parts[1]);
            if (parts.Length > 2)
                version.Patch = int.Parse(parts[2]);

            return version;
        }

        /// <summary>
        /// Compares two versions. If versionA &lt; versionB then this method returns &lt; 0. If
        /// the two versions are equal it returns 0. Otherwise this method returns &gt; 0. 
        /// Comparison order is Major, Minor, Patch, Build, PreRelease then Security.
        /// </summary>
        /// <param name="versionA">The first version to compare</param>
        /// <param name="versionB">The second version to compare</param>
        /// <returns></returns>
        public static int Compare(string? versionA, string? versionB)
        {
            return Compare(Parse(versionA), Parse(versionB));
        }

        /// <summary>
        /// Compares two versions. If versionA &lt; versionB then this method returns &lt; 0. If
        /// the two versions are equal it returns 0. Otherwise this method returns %gt; 0. 
        /// Comparison order is Major, Minor, Patch, Build, PreRelease then Security.
        /// </summary>
        /// <param name="versionA">The first version to compare</param>
        /// <param name="versionB">The second version to compare</param>
        /// <returns></returns>
        public static int Compare(VersionInfo versionA, VersionInfo versionB)
        {
            if (versionA.Major != versionB.Major)
                return versionA.Major - versionB.Major;

            if (versionA.Minor != versionB.Minor)
                return versionA.Minor - versionB.Minor;

            if (versionA.Patch != versionB.Patch)
                return versionA.Patch - versionB.Patch;

            if (versionA.Build != versionB.Build)
                return versionA.Build - versionB.Build;

            // A pre-release string will be greater than an empty string. We actually want the
            // opposite. 2.5.0 > 2.5.0-RC1 and 2.5.0-RTM > 2.5.0-RC1

            if (string.IsNullOrWhiteSpace(versionA.PreRelease) && !string.IsNullOrWhiteSpace(versionB.PreRelease))
                return 1;

            if (!string.IsNullOrWhiteSpace(versionA.PreRelease) && string.IsNullOrWhiteSpace(versionB.PreRelease))
                return -1;

            if (!(versionA.PreRelease ?? string.Empty).Equals(versionB.PreRelease ?? string.Empty))
                return (versionA.PreRelease ?? string.Empty).CompareTo(versionB.PreRelease ?? string.Empty);

            return versionA.SecurityUpdate.CompareTo(versionB.SecurityUpdate);
        }
    }
}
