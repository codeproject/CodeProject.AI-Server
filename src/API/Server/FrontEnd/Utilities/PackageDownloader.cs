using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;

using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend.Utilities
{
    /// <summary>
    /// A utility class for Module package operations
    /// </summary>
    /// <remarks>This class should be registered as a Singleton in DI due to the HttpClient.</remarks>
    public class PackageDownloader
    {
        private readonly VersionConfig _versionConfig;
        private readonly HttpClient    _httpClient;

        /// <summary>
        /// Initializes a new instance of the PackageDownloader class.
        /// </summary>
        /// <param name="versionOptions">The AiServer Version info.</param>
        public PackageDownloader(IOptions<VersionConfig> versionOptions)
        {
            _versionConfig = versionOptions.Value;
            string currentServerVersion = _versionConfig.VersionInfo?.Version ?? string.Empty;
            _httpClient = new() { Timeout = new TimeSpan(0, 0, 30) };

            // Send the current server's version with this call so we can filter the list
            // of available modules based on the current servers version.
            _httpClient.DefaultRequestHeaders.Add("X-CPAI-Server-Version", currentServerVersion);
        }

        /// <summary>
        /// Downloads a text file asynchronously and returns the text
        /// </summary>
        /// <param name="uri">The location of the download</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<string> DownloadTextFileAsync(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentOutOfRangeException($"{nameof(uri)} is null or empty.");

            if (uri.StartsWithIgnoreCase("file://"))
            {
                // remove file:// and then convert /dir -> C:\dir or c:\dir -> /dir as needed
                uri = uri.Substring("file://".Length);
                if (SystemInfo.OperatingSystem.EqualsIgnoreCase("Windows"))
                {
                    if (uri.StartsWith("/"))
                        uri = "C:" + uri;
                }
                else if (uri.StartsWithIgnoreCase("c:\\"))
                    uri = uri.Substring("c:".Length);

                uri = Text.FixSlashes(uri);
                return await File.ReadAllTextAsync(uri);
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri _))
                throw new InvalidOperationException($"{nameof(uri)} is not a valid URI.");

            return await _httpClient.GetStringAsync(uri);
        }

        /// <summary>
        /// Downloads a file asynchronously
        /// </summary>
        /// <param name="uri">The location of the download</param>
        /// <param name="outputPath">Where to write the output</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<(bool, string)> DownloadFileAsync(string uri, string outputPath)
        {
            string error = string.Empty;

            if (string.IsNullOrWhiteSpace(uri))
            {
                error = $"{nameof(uri)} is null or empty.";
                throw new ArgumentOutOfRangeException(error);
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri _))
            {
                error = $"{nameof(uri)} is not a valid URI.";
                throw new InvalidOperationException(error);
            }

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(uri);
                if (fileBytes.Length > 0)
                {
                    File.WriteAllBytes(outputPath, fileBytes);
                    return (true, error);
                }
                else
                    error = "No bytes downloaded";
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.WriteLine(e);
            }

            return (false, error);
        }

        /// <summary>
        /// Unzips an archive into the given directory
        /// </summary>
        /// <param name="archivePath"></param>
        /// <param name="extractDirectory"></param>
        /// <param name="pathsOfFilesExtracted"></param>
        /// <returns></returns>
        public bool Extract(string archivePath, string extractDirectory,
                                   out Collection<string>? pathsOfFilesExtracted)
        {
            bool result = true;

            pathsOfFilesExtracted = null;

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(archivePath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Directory?
                    if (string.IsNullOrEmpty(entry?.Name))
                        continue;

                    string entryFilepath = Text.FixSlashes(entry.FullName);
                    string dirPath = extractDirectory + Path.DirectorySeparatorChar
                                   + Path.GetDirectoryName(entryFilepath);

                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);

                    string extractPath = extractDirectory + Path.DirectorySeparatorChar
                                       + entryFilepath;

                    entry.ExtractToFile(extractPath, true);

                    pathsOfFilesExtracted ??= new Collection<string>();
                    pathsOfFilesExtracted.Add(extractPath);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                result = false;
            }

            return result;
        }
    }
}
