using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Utilities
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
                if (SystemInfo.IsWindows)
                {
                    if (uri.StartsWith("/"))    // url = "file:///Program Files\\CodeProject\\..."
                    {
                        string drive = Directory.GetCurrentDirectory().Split('\\')[0];
                        uri = drive + uri;
                    }
                }
                else 
                {
                    // HACK
                    if (uri.StartsWithIgnoreCase("c:\\"))
                        uri = "/" + uri.Substring("c:\\".Length);
                    else if (uri.StartsWithIgnoreCase("d:\\"))
                        uri = "/" + uri.Substring("d:\\".Length);
                }

                uri = Text.FixSlashes(uri);
                return await File.ReadAllTextAsync(uri).ConfigureAwait(false);
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                throw new InvalidOperationException($"{nameof(uri)} is not a valid URI.");

            return await _httpClient.GetStringAsync(uri).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file asynchronously
        /// </summary>
        /// <param name="uri">The location of the download</param>
        /// <param name="outputPath">Where to write the output</param>
        /// <param name="overwrite">If true, overwrite a file if it already exists</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<(bool, string)> DownloadFileAsync(string uri, string outputPath,
                                                            bool overwrite = true)
        {
            string error = string.Empty;

            if (string.IsNullOrWhiteSpace(uri))
            {
                error = $"{nameof(uri)} is null or empty.";
                throw new ArgumentOutOfRangeException(error);
            }

            if (!overwrite && File.Exists(outputPath))
                return (false, $"File '{outputPath}' exists. Delete first, or set overwrite = true");

            // HACK to prevent us copying files over one another in file: mode
            if (uri.StartsWithIgnoreCase("file://"))
            {
                string localUrlfilename = uri.Substring(7);
                localUrlfilename = Text.FixSlashes(localUrlfilename);

                if (!File.Exists(localUrlfilename))
                    return (false, $"File '{localUrlfilename}' does not exist");

                File.Copy(localUrlfilename, outputPath);
                if (File.Exists(outputPath))
                    return (true, error);

                return (false, "Unable to copy file to destination");
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                error = $"{nameof(uri)} is not a valid URI.";
                throw new InvalidOperationException(error);
            }

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(uri).ConfigureAwait(false);
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
                Debug.WriteLine("Error downloading file: " + error);
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
                Console.WriteLine("Error extracting file: " + e.Message);
                result = false;
            }

            return result;
        }
    }
}
