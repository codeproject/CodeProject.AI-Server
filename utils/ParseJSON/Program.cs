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
            string jsonPath   = "$.Modules.ObjectDetectionYOLOv8.InstallOptions.Platforms";     // A string from an array
            // string jsonPath = "$.Modules.ObjectDetectionYOLOv8.InstallOptions.DownloadableModels[0]";
            // string jsonPath = "$.Modules.#keys[0]";
            string filePath   = "test.json";
            bool   encodeBang = false;
#else
            if (args.Length < 1 || args.Length > 3)
            {
                Usage();
                return;
            }

            string jsonPath   = args[0];
            string filePath   = args.Length > 1 ? args[1] : string.Empty;
            bool   encodeBang = args.Length > 2 ? args[2] == "true" : false;
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
                    string? value;
                    if (extractedValue is string[] valueArray)
                        value = string.Join(",", valueArray);
                    else
                        value = extractedValue?.ToString();

                    // Windows CMD has makes dealing with "!" hard. Here we switch to an alternative
                    if (encodeBang && value is not null)
                       value = value.Replace("!", "ǃ"); // U+0021 -> U+01c3. Or could use ‼

                    Console.WriteLine(value);
                }
            }
        }

        static void Usage()
        {
#if Windows
            Console.WriteLine("Usage: echo '{ json content... }' | ParseJSON 'key' [encode bangs]");
            Console.WriteLine("       ParseJSON 'key' file.json");
            Console.WriteLine("eg echo { \"name\": \"value\" } | ParseJSON $.name");
            Console.WriteLine("encode bangs is true or false, and if true, '!' chars (U+0021) are converted to 'ǃ' chars (U+01c3).");
#else
            Console.WriteLine("Usage: echo '{ json content... }' | dotnet ParseJSON.dll 'key' [encode bangs]");
            Console.WriteLine("       dotnet ParseJSON.dll 'key' file.json");
            Console.WriteLine("eg echo { \\\"name\\\": \\\"value\\\" } | dotnet ParseJSON.dll $.name");
            Console.WriteLine("encode bangs is true or false, and if true, '!' chars (U+0021) are converted to 'ǃ' chars (U+01c3).");
#endif
        }
    }
}