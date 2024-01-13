// #define TEST_INPUT

using System;
using System.Runtime.InteropServices;
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
        static void Main(string[] args)
        {
#if TEST_INPUT
            // string jsonString = "{ \"root\" : \"value\" }";
            // string jsonPath   = "$.root";
            
            string jsonString = System.IO.File.ReadAllText("/home/chris/Dev/CodeProject/CodeProject.AI-Server-Private/src/modules/ALPR/modulesettings.raspberrypi.json");
            Console.WriteLine(jsonString);

            // string jsonPath = "$.Modules.ALPR.Name";         // A string
            string jsonPath    = "$.Modules.ALPR.Platforms";    // An array
            // string jsonPath = "$.Modules.ALPR.Platforms[1]"; // A string from an array
#else
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: echo 'file or text' | ParseJSON 'key'");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine("eg ParseJSON.exe $.root.branch[1] < text.json");
                    Console.WriteLine("eg echo { \"root\": \"value\" } | ParseJSON.exe $.root");
                }
                else
                {
                    Console.WriteLine("eg echo 'text.json' | dotnet ParseJSON $.root.branch[1]");
                    Console.WriteLine("eg echo { \\\"root\\\": \\\"value\\\" } | dotnet ParseJSON $.root");
                }
                return;
            }

            // Get the JSON path from the command line arguments
            string jsonPath = args[0];

            // Read input from stdin
            string? input, jsonString = string.Empty;
            while ((input = Console.ReadLine()) != null)
                jsonString += input;
#endif
            // Console.WriteLine("jsonString = " + jsonString ?? "");

            // Parse and extract
            if (!string.IsNullOrWhiteSpace(jsonString))
            {
                var jsonObject     = DeserializeJson(jsonString);
                var extractedValue = ExtractValue(jsonObject, jsonPath);
                if (extractedValue is not null)
                    Console.WriteLine(extractedValue?.ToString());
            }
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

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling         = JsonCommentHandling.Skip,
                    AllowTrailingCommas         = true,
                    // NumberHandling           = JsonNumberHandling.AllowReadingFromString,
                    Converters                  = { new JsonStringEnumConverter() }
                };

                // "//" comments are not processed properly. /* .. */ are. So strip these out first.
                // We need to ensure "url" : "http://www.bringbackjsoncomments.com", works, but we'd
                // also like "name" : "options", // can be "a" or "b" to work too.
                // Can we please just state that Douglas Crockford was wrong and short-sighted in
                // removing comments from JSON. It didn't achieve his "stop people messing with
                // directives" complaint and simply made matters worse. This is a waste of every
                // developer's time.
                string pattern = @"(//[^""]*$)"; // This isn't good enough.
                jsonString = Regex.Replace(jsonString, pattern, string.Empty, RegexOptions.Multiline);

                return JsonSerializer.Deserialize<JsonNode>(jsonString, options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to deserialize the input: {ex.Message}");
                return null;
            }
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

            // Extract value using JSON path
            var pathSegments = jsonPath.Split(new char[] { '.','[',']' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in pathSegments)
            {
                if (int.TryParse(segment, out int index))
                {
                    // If the segment is an array index
                    jsonNode = jsonNode[index];
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