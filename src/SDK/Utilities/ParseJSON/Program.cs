// #define TEST_INPUT

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CodeProject.AI.Utilities
{
    /// <summary>
    /// The main ParseJSON program
    /// </summary>
    public class ParseJSON
    {
        // On command line:
        //   ParseJSON $.Modules.ALPR-4\.1.Platforms[0] test.json (Windows)
        //   dotnet ParseJSON.dll $.Modules.ALPR-4\.1.Platforms[0] test.json (Linux)

        static void Main(string[] args)
        {
#if TEST_INPUT    
            // string jsonPath = "$.Modules.ALPR-4\\.1.Platforms";     // A string from an array
            string jsonPath = "$.Modules.ObjectDetectionYOLOv8.InstallOptions.DownloadableModels[0]";
            string filePath = "test.json";
#else
            if (args.Length < 1 || args.Length > 2)
            {
                Usage();
                return;
            }

            string jsonPath = args[0];
            string filePath = args.Length > 1 ? args[1] : string.Empty;
#endif

            // Get the JSON path from the command line arguments
            string jsonString = string.Empty;
            if (!string.IsNullOrEmpty(filePath))
            {
                // JSON from file
                try
                {
                    jsonString = System.IO.File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Unable to read from {args[1]}. {ex.Message}");
                }
            }
            else
            {
                // Read JSON from stdin
                string? input;
                while ((input = Console.ReadLine()) != null)
                    jsonString += input;
            }

            // Console.WriteLine("jsonString = " + jsonString ?? "");

            // Parse and extract
            if (!string.IsNullOrWhiteSpace(jsonString))
            {
                JsonNode? jsonObject     = DeserializeJson(jsonString);
                object?   extractedValue = ExtractValue(jsonObject, jsonPath);
                if (extractedValue is not null)
                    Console.WriteLine(extractedValue?.ToString());
            }
        }

        static void Usage()
        {
#if Windows
            Console.WriteLine("Usage: echo '{ json content... }' | ParseJSON 'key'");
            Console.WriteLine("       ParseJSON 'key' file.json");
            Console.WriteLine("eg echo { \"name\": \"value\" } | ParseJSON $.name");
#else
            Console.WriteLine("Usage: echo '{ json content... }' | dotnet ParseJSON.dll 'key'");
            Console.WriteLine("       dotnet ParseJSON.dll 'key' file.json");
            Console.WriteLine("eg echo { \\\"name\\\": \\\"value\\\" } | dotnet ParseJSON.dll $.name");
#endif
        }

        /// <summary>
        /// Deserialize JSON string to an object 
        /// </summary>
        /// <param name="jsonString">The JSON string</param>
        /// <returns>A JsonObject</returns>
        static JsonNode? DeserializeJson(string? jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling         = JsonCommentHandling.Skip,
                AllowTrailingCommas         = true,
                // NumberHandling           = JsonNumberHandling.AllowReadingFromString,
                Converters                  = { new JsonStringEnumConverter() }
            };

            JsonNode? json = null;
            try
            {
                json = JsonSerializer.Deserialize<JsonNode>(jsonString, options);
            }
            catch (Exception jex)
            {
                Console.Error.WriteLine("Unable to parse JSON: " + jex.Message);
            }

            return json;
        }

        /// <summary>
        /// Extracts a value or values from a jsonObject using JSON path format
        /// </summary>
        /// <example>
        /// Console.WriteLine("Name: " + ExtractValue(jsonObject, "$.name"));
        /// Console.WriteLine("City: " + ExtractValue(jsonObject, "$.address.city"));
        /// Console.WriteLine("Second Language: " + ExtractValue(jsonObject, "$.languages[1]"));
        /// </example>
        static object? ExtractValue(JsonNode? jsonNode, string jsonPath)
        {
            if (jsonNode is null)
                return jsonNode;

            if (jsonPath == "$")
                return jsonNode;

            jsonPath = jsonPath.TrimStart('$');

            var pathSegments = Regex.Split(jsonPath, "(?<!\\\\)[.[\\]]");
            foreach (var rawSegment in pathSegments)
            {
                if (string.IsNullOrWhiteSpace(rawSegment))
                    continue;

                string segment = rawSegment.Replace("\\.", ".");

                if (int.TryParse(segment, out int index))
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