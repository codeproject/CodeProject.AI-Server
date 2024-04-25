// #define TEST_INPUT

using System;
using System.Text.Json.Nodes;
using CodeProject.AI.SDK.Utils;

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
            // string jsonPath = "$.Modules.ObjectDetectionYOLOv8.InstallOptions.DownloadableModels[0]";
            string jsonPath = "$.Modules.#keys[0]";
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
            JsonObject? jsonObject = null;

            if (string.IsNullOrEmpty(filePath))
            {
                // Read JSON from stdin
                string? input, jsonString = string.Empty;
                while ((input = Console.ReadLine()) != null)
                    jsonString += input;

                jsonObject = JsonUtils.DeserializeJson(jsonString);

                // Console.WriteLine("jsonString = " + jsonString ?? "");
            }
            else            
            {
                jsonObject = JsonUtils.LoadJson(filePath);
                if (jsonObject is null)
                    Console.Error.WriteLine($"Unable to read from {args[1]}.");
            }

            // Extract value
            if (jsonObject is not null)
            {
                var extractedValue = JsonUtils.ExtractValue(jsonObject, jsonPath);
                if (extractedValue is not null)
                {
                    if (extractedValue is string[] valueArray)
                        Console.WriteLine(string.Join(",", valueArray));
                    else
                        Console.WriteLine(extractedValue?.ToString());
                }
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
    }
}