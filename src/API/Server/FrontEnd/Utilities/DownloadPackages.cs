using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.API.Server.Frontend.Utilities
{
    /// <summary>
    /// A utility class for Module package operations
    /// </summary>
    public class DownloadPackages
    {
        private static readonly HttpClient _httpClient = new() { Timeout = new TimeSpan(0, 0, 30) };

        /// <summary>
        /// Downloads a text file asynchronously and returns the text
        /// </summary>
        /// <param name="uri">The location of the download</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public static async Task<string> DownloadTextFileAsync(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentOutOfRangeException($"{nameof(uri)} is null or empty.");

            if (uri.StartsWithIgnoreCase("file://"))
            {
                uri = uri.Substring("file://".Length);
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
        public static async Task<(bool, string)> DownloadFileAsync(string uri, string outputPath)
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
        public static bool Extract(string archivePath, string extractDirectory,
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
            catch // (Exception e)
            {
                result = false;
            }

            return result;
        }
    }
}
