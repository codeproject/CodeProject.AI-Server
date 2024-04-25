
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CodeProject.AI.SDK.Utils
{
    /// <summary>
    /// Loose collection of JSON utilities.
    /// </summary>
    public class JsonUtils
    {
        /// <summary>
        /// Tests whether the given file contains valid JSON
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>True if the path contains valid JSON; false otherwise</returns>
        public static bool IsJsonFileValid(string path)
        {
            JsonObject? contents = LoadJson(path);
            return contents is not null;
        }

        /// <summary>
        /// Tests whether the given file contains valid JSON
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>True if the path contains valid JSON; false otherwise</returns>
        public async static Task<bool> IsJsonFileValidAsync(string path)
        {
            JsonObject? contents = await LoadJsonAsync(path);
            return contents is not null;
        }

        /// <summary>
        /// Loads a JSON file into a JsonObject. This is a convenience function that will swallow
        /// exceptions and allow comments and trailing commas in the JSON file being loaded.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>A JSON object containing the JSON, or an empty object on error</returns>
        public static JsonObject? LoadJson(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (!File.Exists(path))
                return null;

            try
            {
                string content = File.ReadAllText(path);
                JsonObject? json = DeserializeJson(content);

                return json;
            }
            catch /*(Exception ex)*/
            {
                return null;
            }
        }

        /// <summary>
        /// Loads a JSON file into a JsonObject. This is a convenience function that will swallow
        /// exceptions and allow comments and trailing commas in the JSON file being loaded.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>A JSON object containing the JSON, or an empty object on error</returns>
        public async static Task<JsonObject?> LoadJsonAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (!File.Exists(path))
                return null;

            try
            {
                string content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                JsonObject? json = DeserializeJson(content);

                return json;
            }
            catch /*(Exception ex)*/
            {
                return null;
            }
        }

        /// <summary>
        /// Saves a JsonObject to a file.
        /// </summary>
        /// <param name="json">The JsonObject to save</param>
        /// <param name="path">The path to save</param>
        /// <returns>true on success; false otherwise</returns>
        public static bool SaveJson(JsonObject? json, string path)
        {
            if (json is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return false;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(json, options);

                File.WriteAllText(path, configJson);

                return true;
            }
            catch /*(Exception ex)*/
            {
                // _logger.LogError($"Exception saving module settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves a JsonObject to a file.
        /// </summary>
        /// <param name="json">The JsonObject to save</param>
        /// <param name="path">The path to save</param>
        /// <returns>true on success; false otherwise</returns>
        public async static Task<bool> SaveJsonAsync(JsonObject? json, string path)
        {
            if (json is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return false;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(json, options);

                await File.WriteAllTextAsync(path, configJson).ConfigureAwait(false);

                return true;
            }
            catch /*(Exception ex)*/
            {
                // _logger.LogError($"Exception saving module settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deserialize JSON string to an object 
        /// </summary>
        /// <param name="jsonString">The JSON string</param>
        /// <returns>A JsonObject</returns>
        public static JsonObject? DeserializeJson(string? jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return null;

            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            JsonObject? json = null;
            try
            {
                json = JsonSerializer.Deserialize<JsonObject>(jsonString, options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unable to parse JSON: " + ex.Message);
            }

            return json;
        }

        /// <summary>
        /// Extracts a value or values from a jsonObject using JSON path format
        /// </summary>
        /// <param name="jsonNode">The JsonNode object to start
        /// <paramref name="jsonPath">The JSON path. Special extension here is the use of the segment
        /// "#keys" or #keys[n], which will return an array of property names of the element under
        /// the segment preceding this specifier, or the nth property under the element. 
        /// Eg. "$.Modules.#keys" will return the property names of the "Modules" element.
        ///      { "Modules": { "moduleA" : {...} } } => [ "moduleA" ]
        ///     "$.Modules.#keys[0]" of the same JSON will return "moduleA"
        /// <example>
        /// Console.WriteLine("Name: " + ExtractValue(jsonObject, "$.name"));
        /// Console.WriteLine("City: " + ExtractValue(jsonObject, "$.address.city"));
        /// Console.WriteLine("Second Language: " + ExtractValue(jsonObject, "$.languages[1]"));
        /// </example>
        public static object? ExtractValue(JsonNode? jsonNode, string jsonPath)
        {
            if (jsonNode is null)
                return jsonNode;

            if (jsonPath == "$")
                return jsonNode;

            jsonPath = jsonPath.TrimStart('$');

            var pathSegments = Regex.Split(jsonPath, "(?<!\\\\)[.[\\]]");
            if (pathSegments.Length == 0)
                return null;

            for (int i = 0; i < pathSegments.Length; i++)
            {
                string rawSegment = pathSegments[i];
                if (string.IsNullOrWhiteSpace(rawSegment))
                    continue;

                string segment = rawSegment.Replace("\\.", ".");

                // HACK: for special #keys[n] segment specifier
                if (segment == "#keys")
                {
                    // Get Properties
                    string[] propertyNames = Array.Empty<string>();

                    if (jsonNode.Parent is JsonObject jsonObject &&
                        jsonObject.TryGetPropertyValue("Modules", out JsonNode? valuesNode) &&
                        valuesNode is JsonObject nodes)
                    {
                        propertyNames = nodes.Select(n => n.Key).ToArray();                      
                    }

                    // Are we getting an individual property? ("#keys[n]", so segment i+1 = "n")
                    if (pathSegments.Length > i+1 && 
                        int.TryParse(pathSegments[i + 1], out int index) &&
                        propertyNames.Length > index)
                    {
                        return propertyNames[index];
                    }

                    // No index provided so return entire array of property names if any found
                    if (propertyNames.Length > 0)
                        return propertyNames;

                    jsonNode = null; // fail
                }
                else if (int.TryParse(segment, out int index))
                {
                    // If the segment is an array index
                    if (jsonNode is JsonArray && (jsonNode as JsonArray)!.Count > index)
                        jsonNode = jsonNode[index];
                    else
                        jsonNode = null;
                }
                else if (jsonNode[segment] is not null)
                {
                    // If the segment is an object property
                    jsonNode = jsonNode[segment];
                }
                else
                {
                    jsonNode = null;
                }

                if (jsonNode is null)
                    break;
            }

            return jsonNode;
        }
    }
}