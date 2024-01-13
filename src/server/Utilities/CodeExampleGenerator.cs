using System.Linq;
using System.Text;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.Server.Modules;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Sample code generator
    /// </summary>
    public class CodeExampleGenerator
    {
        /// <summary>
        /// Creates a new instance of the CodeExampleGenerator class
        /// </summary>
        public CodeExampleGenerator()
        {
        }

        /// <summary>
        /// Generates sample code that calls and consumes the given endpoint
        /// </summary>
        /// <param name="routeInfo">The endpoint's route info</param>
        /// <returns>A string</returns>
        public string GenerateJavascript(ModuleRouteInfo routeInfo)
        {
            if (routeInfo.Method != "POST")
                return string.Empty;

            var sample = new StringBuilder("```javascript\n");

            bool hasFileInput  = routeInfo.Inputs?.Any(input => input.Type.ToLower() == "file") == true;
            bool hasFileOutput = routeInfo.ReturnedOutputs?.Any(output => output.Type.ToLower() == "file") == true;

            if (routeInfo.Inputs?.Length > 0)
            {
                if (hasFileInput)
                    sample.AppendLine("// Assume we have a HTML INPUT type=file control with ID=fileChooser");

                sample.AppendLine("var formData = new FormData();");

                int fileCount = 0;
                foreach (RouteParameterInfo input in routeInfo.Inputs!)
                {
                    if (input.Type.ToLower() == "file")
                    {
                        sample.AppendLine($"formData.append('{input.Name}', fileChooser.files[{fileCount++}]);");
                    }
                    else
                    {
                        string value = input.DefaultValue ?? input.Type.ToLower() switch
                        {
                            "text"    => "''",
                            "integer" => "0",
                            "float"   => "0.0",
                            "boolean" => "false",
                            "file"    => "file",
                            "object"  => "null",
                            _         => "null"
                        };
                        sample.AppendLine($"formData.append(\"{input.Name}\", {value});");
                    }
                }
            }
           
            sample.AppendLine($"\nvar url = 'http://localhost:32168/v1/{routeInfo.Route}';\n");

            sample.AppendLine("fetch(url, { method: \"POST\", body: formData})");
            sample.AppendLine("      .then(response => {");
            sample.AppendLine("           if (response.ok) {");
            sample.AppendLine("               response.json().then(data => {");

            if (routeInfo.ReturnedOutputs is not null)
            {
                int imgCount = 1;
                foreach (RouteParameterInfo output in routeInfo.ReturnedOutputs)
                {
                    if (output.Type.ToLower() == "base64imagedata")
                    {
                        sample.AppendLine($"                   // Assume we have an IMG tag named img{imgCount}");
                        sample.AppendLine($"                   img{imgCount++}.src = \"data:image/png;base64,\" + data.{output.Name};");
                    }
                    else
                    {
                        string result = output.Type.ToLower() switch
                        {
                            "text"    => $"data.{output.Name}",
                            "string"  => $"data.{output.Name}",
                            "integer" => $"data.{output.Name}",
                            "float"   => $"data.{output.Name}.toFixed(2)",
                            "boolean" => $"data.{output.Name}",
                            "file"    => $"data.{output.Name}.name",
                            "object"  => $"JSON.stringify(data.{output.Name})",
                            _         => "(nothing returned)"
                        };

                        sample.AppendLine($"                   console.log(\"{output.Name}: \" + {result})");
                    }
                }
            }
            else
                sample.AppendLine($"                   console.log(\"Operation completed succesfully\")");

            sample.AppendLine("               })");
            sample.AppendLine("           }");
            sample.AppendLine("       });");
            sample.AppendLine("        .catch (error => {");
            sample.AppendLine("            console.log('Unable to complete API call: ' + error);");
            sample.AppendLine("       });");


            sample.AppendLine("```");
            
            return sample.ToString();
        }
    }
}
