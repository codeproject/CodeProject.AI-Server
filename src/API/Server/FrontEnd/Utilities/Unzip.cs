using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace CodeProject.AI.API.Server.Frontend.Utilities
{
    /// <summary>
    /// A utility class for Module package operations
    /// </summary>
    public class Packages
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Downloads a file asynchronously
        /// </summary>
        /// <param name="uri">The location of the download</param>
        /// <param name="outputPath">Where to write the output</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public static async void DownloadFileAsync(string uri, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentOutOfRangeException($"{nameof(uri)} is null or empty.");

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri _))
                throw new InvalidOperationException($"{nameof(uri)} is not a valid URI.");

            byte[] fileBytes = await _httpClient.GetByteArrayAsync(uri);
            File.WriteAllBytes(outputPath, fileBytes);
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

                    string entryFilepath = entry.FullName.Replace("/", "\\");
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
